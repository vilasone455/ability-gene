// Sword, staff, spear and hammer variants of the orb armament preview.
// Shared morph and hand attachment; slash, sweep, thrust and heavy strike respectively.
import armament from './six-paths-rifle.js';

const { interval, range, recoil, ...params } = armament.params;
export default {
  ...armament,
  label: 'Orb Melee Weapons (sketch)',
  params: {
    ...params,
    weapon: { ...params.weapon, value: 'Sword', options: ['Sword', 'Staff', 'Spear', 'Hammer'] },
  },
};
