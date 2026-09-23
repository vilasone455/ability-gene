// Unlimited Void: inside, listed a second time with the Cursed Clash speed-line colours as its default,
// so the Compare tab can show the anime and the game colour sets side by side (one lab entry has one set
// of slider values, so the same entry cannot be A and B with different settings). Everything else is
// gojo-unlimited-void-inside.js. Both colour sets are kept for reference (decided 2026-09-23).
import inside from './gojo-unlimited-void-inside.js';

export default {
  ...inside,
  label: 'Unlimited Void: inside, Cursed Clash lines (sketch)',
  params: { ...inside.params, palette: { ...inside.params.palette, value: 'Cursed Clash' } },
};
