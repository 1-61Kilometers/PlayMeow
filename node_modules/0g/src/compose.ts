import { Game } from './Game.js';

export function compose(
  ...systems: ((game: Game) => () => void)[]
): (game: Game) => () => void {
  return (game: Game) => {
    const cleanups = systems.map((sys) => sys(game));
    return () => {
      cleanups.forEach((cleanup) => cleanup());
    };
  };
}
