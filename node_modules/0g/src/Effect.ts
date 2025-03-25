import { Game } from './Game.js';
import { QueryComponentFilter } from './Query.js';
import { EntityImpostorFor } from './QueryIterator.js';
import { allSystems } from './System.js';

type CleanupFn = () => void | Promise<void>;
type CleanupResult = Promise<CleanupFn | void> | CleanupFn | void;

export function effect<Filter extends QueryComponentFilter>(
  filter: Filter,
  effect: (
    entity: EntityImpostorFor<Filter>,
    game: Game,
    info: { abortSignal: AbortSignal },
  ) => CleanupResult,
) {
  function eff(game: Game) {
    const query = game.queryManager.create(filter);
    const abortControllers = new Array<AbortController>();
    const cleanups = new Array<CleanupFn>();

    async function onEntityAdded(entityId: number) {
      const entity = game.get(entityId);
      if (!entity) {
        throw new Error(
          `Effect triggered for entity ${entityId}, but it was not found`,
        );
      }
      const abortController = new AbortController();
      abortControllers[entityId] = abortController;
      const result = effect(entity, game, {
        abortSignal: abortController.signal,
      });
      if (result instanceof Promise) {
        cleanups[entityId] = () => {
          result.then((clean) => {
            clean?.();
          });
        };
      } else if (result) {
        cleanups[entityId] = result;
      }
    }

    async function onEntityRemoved(entityId: number) {
      abortControllers[entityId]?.abort();
      cleanups[entityId]?.();
    }

    const unsubscribes = [
      query.subscribe('entityAdded', onEntityAdded),
      query.subscribe('entityRemoved', onEntityRemoved),
    ];

    return () => {
      for (const unsubscribe of unsubscribes) {
        unsubscribe();
      }
      for (const cleanup of cleanups) {
        cleanup();
      }
    };
  }

  allSystems.push(eff);
  return eff;
}

/** @deprecated - use effect */
export const makeEffect = effect;
