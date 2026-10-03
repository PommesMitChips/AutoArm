/* Shared assembly, template and export logic. No third-party dependencies. */
(function (scope) {
  'use strict';
  const NS = 'http://www.w3.org/2000/svg';
  const colour = value => value === null || /^#[\da-f]{6}$/i.test(value);
  const copy = value => JSON.parse(JSON.stringify(value));
  const number = (value, min, max, label) => {
    if (!Number.isFinite(value) || value < min || value > max) throw new Error(`${label} must be between ${min} and ${max}.`);
    return value;
  };
  const slug = value => String(value).normalize('NFKD').replace(/[^a-z0-9._-]+/gi, '-').replace(/^[.-]+|[.-]+$/g, '').slice(0, 100) || 'assembly';
  const hexColour = value => typeof value === 'string' && /^#[\da-f]{6}$/i.test(value);
  const identifier = (value, fallback, label) => {
    if (value === undefined) return fallback;
    if (typeof value !== 'string' || !value.trim()) throw new Error(`${label} must be a non-empty string.`);
    return value;
  };

  function movePanel(panels, fromIndex, toIndex) {
    if (!Array.isArray(panels) || !Number.isInteger(fromIndex) || !Number.isInteger(toIndex) || fromIndex < 0 || toIndex < 0 || fromIndex >= panels.length || toIndex >= panels.length) throw new Error('Panel positions must be valid zero-based indices.');
    const result = panels.slice();
    result.splice(toIndex, 0, result.splice(fromIndex, 1)[0]);
    return result;
  }

  function snapPosition(rect, peers = [], options = {}) {
    const x = number(rect.x, -Infinity, Infinity, 'Panel x'), y = number(rect.y, -Infinity, Infinity, 'Panel y');
    const width = number(rect.width, Number.MIN_VALUE, Infinity, 'Panel width'), height = number(rect.height, Number.MIN_VALUE, Infinity, 'Panel height');
    const minX = number(options.minX ?? 0, -Infinity, Infinity, 'Minimum x'), minY = number(options.minY ?? 0, -Infinity, Infinity, 'Minimum y');
    const right = options.maxX === undefined ? Infinity : number(options.maxX, minX + width, Infinity, 'Maximum x');
    const bottom = options.maxY === undefined ? Infinity : number(options.maxY, minY + height, Infinity, 'Maximum y');
    const tolerance = number(options.tolerance ?? 10, 0, Infinity, 'Snap tolerance');
    const limits = {x: [minX, right - width], y: [minY, bottom - height]};
    const result = {x: Math.min(limits.x[1], Math.max(minX, x)), y: Math.min(limits.y[1], Math.max(minY, y)), guides: []};
    if (options.enabled === false) return result;
    for (const axis of ['x', 'y']) {
      const size = axis === 'x' ? width : height;
      let closest = null;
      for (const peer of peers) {
        const peerStart = peer[axis], peerSize = peer[axis === 'x' ? 'width' : 'height'];
        if (!Number.isFinite(peerStart) || !Number.isFinite(peerSize) || peerSize <= 0) continue;
        for (const edge of [peerStart, peerStart + peerSize]) for (const offset of [0, size]) {
          const position = edge - offset, distance = Math.abs(position - result[axis]);
          if (position >= limits[axis][0] && position <= limits[axis][1] && distance <= tolerance && (!closest || distance < closest.distance)) closest = {position, distance, edge};
        }
      }
      if (closest) {result[axis] = closest.position; result.guides.push({axis, value: closest.edge});}
    }
    return result;
  }

  function theme(input = {}) {
    if (!input || typeof input !== 'object' || Array.isArray(input)) throw new Error('A colour theme must be an object.');
    const result = {};
    for (const key of ['text', 'line']) if (Object.hasOwn(input, key)) {
      if (!colour(input[key])) throw new Error(`${key} colour must be a six-digit hex colour or null for the original colour.`);
      result[key] = input[key];
    }
    if (input.lineScope !== undefined) {
      if (!['rules', 'all'].includes(input.lineScope)) throw new Error('Line scope must be rules or all.');
      result.lineScope = input.lineScope;
    }
    return result;
  }

  function validateTemplate(input, assets) {
    if (!input || input.version !== 1 || !Array.isArray(input.groups) || !input.groups.length || input.groups.length > 100) throw new Error('Use a version 1 template with 1–100 assembly groups.');
    const result = {version: 1, name: String(input.name || 'My assembly').slice(0, 200), theme: theme(input.theme), scale: number(input.scale ?? 1, 0.25, 4, 'Export scale'), groups: [], colourGroups: []};
    const ids = new Set(), panelIDs = new Set();
    let total = 0;
    for (const [index, group] of input.groups.entries()) {
      if (!group || !Array.isArray(group.panels)) throw new Error(`Group ${index + 1} needs a panels array.`);
      const id = identifier(group.id, `group-${index + 1}`, 'Group ID');
      if (ids.has(id)) throw new Error('Every group needs a unique ID.');
      ids.add(id);
      const layout = group.layout || 'vertical';
      if (!['vertical', 'horizontal', 'poster'].includes(layout)) throw new Error('Layout must be vertical, horizontal or poster.');
      const width = number(group.width ?? 620, 100, 8000, 'Group width');
      const gap = number(group.gap ?? 20, 0, 2000, 'Panel spacing');
      const padding = number(group.padding ?? 0, 0, 2000, 'Group padding');
      if (padding * 2 >= width) throw new Error('Padding must leave room for the panels.');
      const minHeight = number(group.minHeight ?? 900, 0, Infinity, 'Poster minimum height');
      if (group.snap !== undefined && typeof group.snap !== 'boolean') throw new Error('Poster snapping must be true or false.');
      const panels = group.panels.map((entry, panelIndex) => {
        if (typeof entry === 'string') entry = {asset: entry};
        if (!entry || !assets.has(entry.asset)) throw new Error(`Missing SVG: ${entry?.asset || `panel ${panelIndex + 1}`}. Import its SVG first.`);
        const panelID = identifier(entry.id, `${id}-panel-${panelIndex}`, 'Panel ID');
        if (panelIDs.has(panelID)) throw new Error('Every panel placement needs a unique ID.');
        panelIDs.add(panelID);
        const elements = {};
        for (const [key, override] of Object.entries(entry.elements || {})) {
          if (!/^node-\d+$/.test(key) || !override || typeof override !== 'object') throw new Error('An element override has an invalid key.');
          elements[key] = {};
          for (const property of ['fill', 'stroke']) if (override[property] !== undefined) {
            if (override[property] !== 'original' && !hexColour(override[property])) throw new Error('Element colours must be six-digit hex colours or original.');
            elements[key][property] = override[property];
          }
        }
        const panel = {id: panelID, asset: entry.asset, theme: theme(entry.theme), elements};
        for (const key of ['x', 'y', 'width']) if (entry[key] !== undefined) panel[key] = number(entry[key], key === 'width' ? Number.MIN_VALUE : -Infinity, Infinity, `Panel ${key}`);
        return panel;
      });
      total += panels.length;
      if (total > 500) throw new Error('A template can contain up to 500 panel placements.');
      const normalized = {id, name: String(group.name || `Group ${index + 1}`).slice(0, 200), filename: slug(group.filename || group.name || id), layout, width, gap, padding, minHeight, snap: group.snap ?? true, theme: theme(group.theme), panels};
      if (layout === 'poster') layoutGroup(normalized, assets);
      result.groups.push(normalized);
    }
    if (input.colourGroups !== undefined && !Array.isArray(input.colourGroups)) throw new Error('Colour groups must be an array.');
    const colourIDs = new Set(), colourNames = new Set(), memberships = new Set(), elementKeys = new Map();
    for (const colourGroup of input.colourGroups || []) {
      if (!colourGroup || typeof colourGroup !== 'object') throw new Error('A colour group must be an object.');
      const id = identifier(colourGroup.id, undefined, 'Colour group ID');
      if (!id || colourIDs.has(id)) throw new Error('Every colour group needs a unique ID.');
      colourIDs.add(id);
      if (typeof colourGroup.name !== 'string' || !colourGroup.name.trim() || colourGroup.name.length > 200) throw new Error('A colour group needs a name of 1–200 characters.');
      const name = colourGroup.name.trim(), nameKey = name.toLowerCase();
      if (colourNames.has(nameKey)) throw new Error('Every colour group needs a unique name.');
      colourNames.add(nameKey);
      if (!hexColour(colourGroup.colour)) throw new Error('Colour group colours must be six-digit hex colours.');
      if (!Array.isArray(colourGroup.members)) throw new Error('A colour group needs a members array.');
      const members = colourGroup.members.map(member => {
        if (!member || typeof member !== 'object' || !/^node-\d+$/.test(member.element) || !['fill', 'stroke'].includes(member.paint)) throw new Error('A colour group member needs a valid element key and fill or stroke paint.');
        const owner = result.groups.find(group => group.id === member.group), panel = owner?.panels.find(panel => panel.id === member.panel);
        if (!panel) throw new Error('A colour group member references a missing group or panel placement.');
        const asset = assets.get(panel.asset);
        if (typeof asset.svg === 'string') {
          if (!elementKeys.has(panel.asset)) {
            const keys = typeof DOMParser !== 'undefined' ? [...parseSVG(asset.svg).root.querySelectorAll('[data-edit-key]')].map(node => node.getAttribute('data-edit-key')) : [...asset.svg.matchAll(/\bdata-edit-key\s*=\s*["'](node-\d+)["']/g)].map(match => match[1]);
            elementKeys.set(panel.asset, new Set(keys));
          }
          if (!elementKeys.get(panel.asset).has(member.element)) throw new Error('A colour group member references a missing SVG element.');
        }
        const key = JSON.stringify([member.group, member.panel, member.element, member.paint]);
        if (memberships.has(key)) throw new Error('Each element paint can belong to only one colour group; duplicate memberships are not allowed.');
        memberships.add(key);
        return {group: member.group, panel: member.panel, element: member.element, paint: member.paint};
      });
      result.colourGroups.push({id, name, colour: colourGroup.colour, members});
    }
    return result;
  }

  function layoutGroup(group, assets) {
    const inner = group.width - 2 * group.padding;
    if (group.layout === 'poster') {
      const minHeight = number(group.minHeight ?? 900, 0, Infinity, 'Poster minimum height');
      let nextY = group.padding, extent = 0, canvasWidth = group.width;
      const placements = group.panels.map((panel, index) => {
        const asset = assets.get(panel.asset);
        if (!asset) throw new Error(`Missing SVG: ${panel.asset}`);
        const width = number(panel.width ?? inner, Number.MIN_VALUE, Infinity, 'Poster panel width');
        const x = number(panel.x ?? group.padding, group.padding, Infinity, 'Poster panel x');
        const y = number(panel.y ?? nextY, group.padding, Infinity, 'Poster panel y');
        const height = number(width * asset.height / asset.width, Number.MIN_VALUE, Infinity, 'Poster panel height');
        extent = Math.max(extent, y + height);
        canvasWidth = Math.max(canvasWidth, number(x + width + group.padding, 0, Infinity, 'Poster canvas width'));
        nextY = Math.max(nextY, y + height + group.gap);
        return {index, panel, x, y, width, height};
      });
      return {width: canvasWidth, height: number(Math.max(minHeight, extent + group.padding), 0, Infinity, 'Poster canvas height'), placements};
    }
    if (!group.panels.length) return {width: group.width, height: 0, placements: []};
    const cell = group.layout === 'horizontal' ? (inner - group.gap * (group.panels.length - 1)) / group.panels.length : inner;
    if (cell < 1) throw new Error('This horizontal group is too crowded. Increase its width or reduce spacing.');
    let y = group.padding, maxHeight = 0;
    const placements = group.panels.map((panel, index) => {
      const asset = assets.get(panel.asset);
      if (!asset) throw new Error(`Missing SVG: ${panel.asset}`);
      const height = cell * asset.height / asset.width;
      const placement = {index, panel, x: group.layout === 'horizontal' ? group.padding + index * (cell + group.gap) : group.padding, y: group.layout === 'horizontal' ? group.padding : y, width: cell, height};
      y += height + group.gap;
      maxHeight = Math.max(maxHeight, height);
      return placement;
    });
    return {width: group.width, height: Math.ceil(group.layout === 'horizontal' ? maxHeight + 2 * group.padding : y - group.gap + group.padding), placements};
  }

  function parseSVG(source) {
    const doc = new DOMParser().parseFromString(source, 'image/svg+xml');
    if (doc.querySelector('parsererror') || doc.documentElement.localName !== 'svg') throw new Error('This file is not a valid SVG.');
    const root = doc.documentElement;
    // Imported files are drawings, so discard active document content.
    root.querySelectorAll('script, foreignObject').forEach(node => node.remove());
    for (const node of [root, ...root.querySelectorAll('*')]) {
      for (const attribute of [...node.attributes]) {
        if (/^on/i.test(attribute.name)) node.removeAttribute(attribute.name);
        if (['data-panel-instance', 'data-panel-id', 'data-ui-layer'].includes(attribute.name)) node.removeAttribute(attribute.name);
        if (/(?:^|:)href$/.test(attribute.name) && !/^(?:#|data:image\/(?:png|jpeg|webp);base64,)/i.test(attribute.value)) node.removeAttribute(attribute.name);
      }
      if (node.localName === 'style') node.textContent = node.textContent.replace(/@import[^;]*;/gi, '').replace(/url\(\s*(['"]?)(?:https?:|file:|\/\/)[^)]*\)/gi, 'none');
    }
    const view = (root.getAttribute('viewBox') || '').trim().split(/[\s,]+/).map(Number);
    let width, height;
    if (view.length === 4 && view.every(Number.isFinite) && view[2] > 0 && view[3] > 0) [width, height] = view.slice(2);
    else {
      width = parseFloat(root.getAttribute('width')); height = parseFloat(root.getAttribute('height'));
      if (!(width > 0 && height > 0)) throw new Error('The SVG needs a viewBox or numeric width and height.');
      root.setAttribute('viewBox', `0 0 ${width} ${height}`);
    }
    const nodes = [...root.querySelectorAll('*')], used = new Set();
    let duplicateKeys = false;
    for (const node of nodes) if (node.hasAttribute('data-edit-key')) {
      const key = node.getAttribute('data-edit-key');
      if (!/^node-\d+$/.test(key)) node.removeAttribute('data-edit-key');
      else if (used.has(key)) duplicateKeys = true;
      else used.add(key);
    }
    if (duplicateKeys) {nodes.forEach(node => node.removeAttribute('data-edit-key')); used.clear();}
    let index = 0;
    // Preserve legacy text/stroke numbering before exposing fill shapes and text spans.
    for (const node of nodes) {
      if (!node.hasAttribute('data-edit-key') && (node.localName === 'text' || (node.hasAttribute('stroke') && node.getAttribute('stroke') !== 'none') || (node.style.stroke && node.style.stroke !== 'none'))) {
        while (used.has(`node-${index}`)) index++;
        const key = `node-${index++}`;
        node.setAttribute('data-edit-key', key); used.add(key);
      }
    }
    index = Math.max(-1, ...[...used].map(key => Number(key.slice(5)))) + 1;
    const drawables = new Set(['polygon', 'rect', 'path', 'circle', 'ellipse', 'line', 'polyline']);
    const assignAdditionalKeys = eligible => {
      for (const node of nodes) {
        if (node.hasAttribute('data-edit-key') || !eligible(node) || node.closest('defs, clipPath, mask, marker, symbol')) continue;
        let hidden = false;
        for (let ancestor = node; ancestor; ancestor = ancestor.parentElement) {
          const display = ancestor.style.display || ancestor.getAttribute('display'), visibility = ancestor.style.visibility || ancestor.getAttribute('visibility'), opacity = ancestor.style.opacity || ancestor.getAttribute('opacity');
          if (display === 'none' || visibility === 'hidden' || visibility === 'collapse' || opacity !== null && opacity !== '' && Number(opacity) === 0) {hidden = true; break;}
        }
        if (!hidden) node.setAttribute('data-edit-key', `node-${index++}`);
      }
    };
    assignAdditionalKeys(node => drawables.has(node.localName));
    // Text spans come last so earlier fill-shape keys remain stable for raw SVGs.
    assignAdditionalKeys(node => node.localName === 'tspan' && node.textContent.trim() && !node.querySelectorAll('tspan').length);
    return {root, width, height};
  }

  function assetFromSVG(id, name, source, category = 'Imported SVGs') {
    const {root, width, height} = parseSVG(source);
    return {id, name, category, width, height, svg: new XMLSerializer().serializeToString(root), custom: true};
  }

  function applyPaint(node, property, value) {
    node.setAttribute(property, value); node.style.setProperty(property, value);
    if (property === 'fill' && node.localName === 'text') node.querySelectorAll('tspan').forEach(span => {span.setAttribute('fill', value); span.style.fill = value;});
  }

  function sourcePaint(node, paint) {
    if (!['fill', 'stroke'].includes(paint)) throw new Error('Source paint must be fill or stroke.');
    const defaultPaint = paint === 'fill' ? '#000000' : 'none';
    for (let ancestor = node; ancestor; ancestor = ancestor.parentElement) {
      const value = (ancestor.style[paint] || ancestor.getAttribute(paint) || '').trim();
      if (!value || ['inherit', 'unset'].includes(value.toLowerCase())) continue;
      return value.toLowerCase() === 'initial' ? defaultPaint : value;
    }
    return defaultPaint;
  }

  function applyColourGroups(root, groupID, panelID, colourGroups = []) {
    const nodes = new Map([...root.querySelectorAll('[data-edit-key]')].map(node => [node.getAttribute('data-edit-key'), node]));
    for (const colourGroup of colourGroups) for (const member of colourGroup.members) {
      if (member.group === groupID && member.panel === panelID && nodes.has(member.element)) applyPaint(nodes.get(member.element), member.paint, colourGroup.colour);
    }
  }

  function applyTheme(root, effective, elements = {}) {
    // Resolve original paints before a theme changes source attributes or inheritance.
    const overrides = [];
    root.querySelectorAll('[data-edit-key]').forEach(node => {
      for (const [property, value] of Object.entries(elements[node.getAttribute('data-edit-key')] || {})) {
        const original = value === 'original';
        overrides.push({node, property, value: original ? sourcePaint(node, property) : value, spans: original && property === 'fill' && node.localName === 'text' ? [...node.querySelectorAll('tspan')].map(span => ({node: span, value: sourcePaint(span, 'fill')})) : []});
      }
    });
    if (effective.text) root.querySelectorAll('text, text tspan').forEach(node => { node.setAttribute('fill', effective.text); node.style.fill = effective.text; });
    if (effective.line) root.querySelectorAll('[data-edit-key]').forEach(node => {
      const stroke = node.getAttribute('stroke') || node.style.stroke;
      const rulesOnly = effective.lineScope !== 'all';
      const inArtwork = node.closest('g[id^="artwork-"]');
      if (stroke && stroke !== 'none' && (!rulesOnly || (!inArtwork && ['line', 'polyline'].includes(node.localName)))) {
        node.setAttribute('stroke', effective.line); node.style.stroke = effective.line;
      }
    });
    for (const {node, property, value, spans} of overrides) {
      applyPaint(node, property, value);
      for (const span of spans) applyPaint(span.node, 'fill', span.value);
    }
  }

  function prefixIDs(root, prefix) {
    const mapping = new Map();
    for (const node of [root, ...root.querySelectorAll('[id]')]) if (node.id) {
      const original = node.id;
      if (mapping.has(original)) throw new Error(`The SVG has duplicate ID ${original}. Give its objects unique IDs before importing.`);
      mapping.set(original, `${prefix}-${original}`);
      node.id = mapping.get(original);
    }
    for (const node of [root, ...root.querySelectorAll('*')]) {
      for (const attribute of [...node.attributes]) {
        if (attribute.name === 'id') continue;
        let value = attribute.value.replace(/url\(\s*(['"]?)#([^)'"\s]+)\1\s*\)/g, (_, quote, id) => `url(#${mapping.get(id) || id})`);
        if (/(?:^|:)href$/.test(attribute.name) && value.startsWith('#')) value = '#' + (mapping.get(value.slice(1)) || value.slice(1));
        if (['aria-labelledby', 'aria-describedby'].includes(attribute.name)) value = value.split(/\s+/).map(id => mapping.get(id) || id).join(' ');
        node.setAttribute(attribute.name, value);
      }
      if (node.localName === 'style') node.textContent = node.textContent.replace(/#([\w.-]+)/g, (match, id) => mapping.has(id) ? '#' + mapping.get(id) : match);
    }
  }

  function svgRoot(width, height, fontCSS) {
    const root = document.createElementNS(NS, 'svg');
    root.setAttribute('xmlns', NS); root.setAttribute('width', width); root.setAttribute('height', height); root.setAttribute('viewBox', `0 0 ${width} ${height}`);
    if (fontCSS) { const defs = document.createElementNS(NS, 'defs'); const style = document.createElementNS(NS, 'style'); style.textContent = fontCSS; defs.append(style); root.append(defs); }
    return root;
  }

  function composeGroup(group, assets, globalTheme = {}, fontCSS = '', prefix = 'group', colourGroups = []) {
    const layout = layoutGroup(group, assets);
    if (!layout.placements.length) throw new Error('This group is empty. Add a panel before exporting.');
    const root = svgRoot(layout.width, layout.height, fontCSS);
    for (const placement of layout.placements) {
      const panel = placement.panel;
      const art = parseSVG(assets.get(panel.asset).svg).root;
      applyTheme(art, {...globalTheme, ...group.theme, ...panel.theme}, panel.elements);
      applyColourGroups(art, group.id, panel.id, colourGroups);
      prefixIDs(art, `${prefix}-panel-${placement.index}`);
      for (const key of ['x', 'y', 'width', 'height']) art.setAttribute(key, placement[key]);
      art.setAttribute('preserveAspectRatio', 'xMidYMid meet');
      art.setAttribute('data-panel-instance', placement.index);
      if (panel.id !== undefined) art.setAttribute('data-panel-id', panel.id);
      root.append(art);
    }
    return {root, width: layout.width, height: layout.height};
  }

  function composeAll(template, assets, fontCSS = '') {
    const groups = template.groups.filter(group => group.panels.length);
    if (!groups.length) throw new Error('Add a panel to an assembly group before exporting.');
    const rendered = groups.map((group, index) => composeGroup(group, assets, template.theme, '', `assembly-${index}`, template.colourGroups));
    const width = Math.max(...rendered.map(group => group.width));
    const gap = 24;
    const height = rendered.reduce((sum, group) => sum + group.height, 0) + gap * (rendered.length - 1);
    const root = svgRoot(width, height, fontCSS);
    let y = 0;
    rendered.forEach(group => { group.root.setAttribute('x', (width - group.width) / 2); group.root.setAttribute('y', y); root.append(group.root); y += group.height + gap; });
    return {root, width, height};
  }

  function pixelSize(width, height, scale) {
    const w = Math.ceil(width * scale), h = Math.ceil(height * scale);
    if (w > 32760 || h > 32760 || w * h > 64000000) throw new Error(`The PNG would be ${w} × ${h} px, too large for reliable browser export. Reduce width or scale, or choose each group as separate PNGs.`);
    return {width: w, height: h};
  }

  const crcTable = Array.from({length: 256}, (_, value) => { for (let bit = 0; bit < 8; bit++) value = value & 1 ? 0xedb88320 ^ value >>> 1 : value >>> 1; return value >>> 0; });
  function crc32(data) { let crc = 0xffffffff; for (const byte of data) crc = crcTable[(crc ^ byte) & 255] ^ crc >>> 8; return (crc ^ 0xffffffff) >>> 0; }
  function zipFiles(files) {
    const encoder = new TextEncoder(), parts = [], central = [];
    let offset = 0;
    for (const file of files) {
      const name = encoder.encode(file.name), data = file.data, crc = crc32(data);
      const local = new Uint8Array(30 + name.length), view = new DataView(local.buffer);
      view.setUint32(0, 0x04034b50, true); view.setUint16(4, 20, true); view.setUint16(6, 0x800, true); view.setUint16(12, 0x21, true);
      view.setUint32(14, crc, true); view.setUint32(18, data.length, true); view.setUint32(22, data.length, true); view.setUint16(26, name.length, true); local.set(name, 30);
      parts.push(local, data);
      const header = new Uint8Array(46 + name.length), directory = new DataView(header.buffer);
      directory.setUint32(0, 0x02014b50, true); directory.setUint16(4, 20, true); directory.setUint16(6, 20, true); directory.setUint16(8, 0x800, true); directory.setUint16(14, 0x21, true);
      directory.setUint32(16, crc, true); directory.setUint32(20, data.length, true); directory.setUint32(24, data.length, true); directory.setUint16(28, name.length, true); directory.setUint32(42, offset, true); header.set(name, 46);
      central.push(header); offset += local.length + data.length;
    }
    const centralSize = central.reduce((sum, part) => sum + part.length, 0);
    const end = new Uint8Array(22), tail = new DataView(end.buffer);
    tail.setUint32(0, 0x06054b50, true); tail.setUint16(8, files.length, true); tail.setUint16(10, files.length, true); tail.setUint32(12, centralSize, true); tail.setUint32(16, offset, true);
    return new Blob([...parts, ...central, end], {type: 'application/zip'});
  }

  const API = {NS, copy, slug, theme, validateTemplate, layoutGroup, movePanel, snapPosition, parseSVG, assetFromSVG, sourcePaint, applyTheme, applyColourGroups, prefixIDs, composeGroup, composeAll, pixelSize, crc32, zipFiles};
  if (typeof module !== 'undefined' && module.exports) module.exports = API;
  else scope.PanelCore = API;
})(typeof window !== 'undefined' ? window : globalThis);
