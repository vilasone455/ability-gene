// A sketch's configuration as text: what the Params tab's copy buttons put on the clipboard.
//
// Three shapes, because they are read by three different things:
//   csharpConstants  lines to paste into a timing class, named as the C# will name them
//   plainList        the values as the page shows them, for a person
//   shareLink        a URL that opens this sketch at these values in someone else's lab
//
// All three walk every parameter the sketch declares, in the order it declares them, under the
// same group headings the panel shows -- so what is copied can be checked against the sliders
// line by line, and a parameter added to a sketch appears here without anything being edited.

/** [[group, [[key, param, value], ...]], ...] in declaration order, as the panel groups them. */
export function groupedValues(module, values) {
  const groups = new Map();
  for (const [key, param] of Object.entries(module.params)) {
    if (!groups.has(param.group)) groups.set(param.group, []);
    groups.get(param.group).push([key, param, values[key]]);
  }
  return [...groups];
}

const constantName = (key) => key[0].toUpperCase() + key.slice(1);

const format = (value) => {
  if (typeof value === 'boolean') return String(value);
  if (typeof value === 'number') return String(Number(value));
  return String(value);
};

/**
 * The values as C# constants, grouped and commented. A number becomes a float, a checkbox a
 * bool, and a choice a comment: a set of options is a switch in the sketch, and what the C#
 * should do with it is a decision, not a constant.
 *
 * The UI's own label follows each line when it differs from the constant name, so a pasted block
 * can still be read against the panel.
 */
export function csharpConstants(module, values, label) {
  const out = [];
  if (label) out.push(`// ${label}`);
  for (const [group, items] of groupedValues(module, values)) {
    out.push(`${out.length ? '\n' : ''}// ${group}`);
    for (const [key, param, value] of items) {
      const name = constantName(key);
      const note = param.label && param.label.replace(/[^a-z0-9]/gi, '').toLowerCase() !== name.toLowerCase()
        ? `   // ${param.label}` : '';
      if (typeof value === 'boolean') out.push(`public const bool ${name} = ${value};${note}`);
      else if (typeof value === 'number') out.push(`public const float ${name} = ${Number(value)}f;${note}`);
      else out.push(`// ${name}: ${value}${note}`);
    }
  }
  return out.join('\n');
}

/** The same values as the panel shows them: group headings, UI labels, plain numbers. */
export function plainList(module, values, label) {
  const out = label ? [label, ''] : [];
  for (const [group, items] of groupedValues(module, values)) {
    out.push(`${group}`);
    for (const [, param, value] of items) out.push(`  ${param.label}: ${format(value)}`);
    out.push('');
  }
  return out.join('\n').trimEnd();
}

/**
 * A link that opens this sketch at these values. Every parameter is written out, not only the
 * ones that differ from the defaults, so the link keeps working when a sketch's defaults change
 * under it.
 */
export function shareLink(origin, effect, module, values, seconds) {
  const query = new URLSearchParams({ effect });
  if (Number.isFinite(seconds)) query.set('t', Number(seconds).toFixed(3));
  for (const [key, , value] of groupedValues(module, values).flatMap(([, items]) => items))
    query.set(`p.${key}`, format(value));
  return `${origin}?${query}`;
}

/** How many parameters a sketch has, for the button that says what it copied. */
export function countParams(module) {
  return Object.keys(module.params).length;
}
