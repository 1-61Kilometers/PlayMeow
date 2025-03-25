import { Game } from './index.js';
import { allSystems } from './System.js';

export function setup(setup: (game: Game) => void | (() => void)) {
  allSystems.push(setup);
  return setup;
}
