import { Game } from './Game.js';
import { Archetype } from './Archetype.js';
import { Filter, isFilter, has } from './filters.js';
import { EntityImpostorFor, QueryIterator } from './QueryIterator.js';
import { Entity } from './Entity.js';
import { EventSubscriber } from '@a-type/utils';
import { ComponentHandle } from './Component2.js';

export type QueryComponentFilter = Array<
  Filter<ComponentHandle> | ComponentHandle
>;

export type QueryEvents = {
  entityAdded(entityId: number): void;
  entityRemoved(entityId: number): void;
  destroy(): void;
};

type ExtractQueryDef<Q extends Query<any>> =
  Q extends Query<infer Def> ? Def : never;

export type QueryIteratorFn<Q extends Query<any>, Returns = void> = {
  (ent: EntityImpostorFor<ExtractQueryDef<Q>>): Returns;
};

export class Query<
  FilterDef extends QueryComponentFilter,
> extends EventSubscriber<QueryEvents> {
  public filter: Filter<ComponentHandle>[] = [];
  readonly archetypes = new Array<Archetype>();
  private trackedEntities: number[] = [];
  private addedThisFrame: number[] = [];
  private removedThisFrame: number[] = [];
  private changesThisFrame = 0;
  private addedIterable: {
    [Symbol.iterator]: () => AddedIterator<FilterDef>;
  };
  private unsubscribes: (() => void)[] = [];
  private unsubscribeArchetypes: (() => void) | undefined = undefined;
  private _generation = 0;
  get generation() {
    return this._generation;
  }

  constructor(private game: Game) {
    super();
    this.addedIterable = {
      [Symbol.iterator]: () => new AddedIterator<FilterDef>(game, this),
    };
    // when do we reset the frame-specific tracking?
    // right before we populate new values from this frame's operations.
    this.unsubscribes.push(
      game.subscribe('preApplyOperations', this.resetStepTracking),
    );
    // after we apply operations and register all changes for the frame,
    // we do processing of final add/remove list
    this.unsubscribes.push(
      game.subscribe('stepComplete', this.processAddRemove),
    );
  }

  private processDef = (userDef: QueryComponentFilter) => {
    return userDef.map((fil) => (isFilter(fil) ? fil : has(fil)));
  };

  initialize(def: FilterDef) {
    this.filter = this.processDef(def);

    Object.values(this.game.archetypeManager.archetypes).forEach(
      this.matchArchetype,
    );
    this.unsubscribeArchetypes = this.game.archetypeManager.subscribe(
      'archetypeCreated',
      this.matchArchetype,
    );

    // reset all tracking arrays
    this.trackedEntities.length = 0;
    this.addedThisFrame.length = 0;
    this.removedThisFrame.length = 0;
    this.changesThisFrame = 0;
    // bootstrap entities list -
    // TODO: optimize?
    for (const ent of this) {
      this.trackedEntities.push(ent.id);
      this.addedThisFrame.push(ent.id);
      this.emitAdded(ent.id);
    }
  }

  private matchArchetype = (archetype: Archetype) => {
    let match = true;
    for (const filter of this.filter) {
      switch (filter.kind) {
        case 'has':
          match = archetype.includes(filter.Component);
          break;
        case 'not':
          match = archetype.omits(filter.Component);
          break;
        case 'changed':
          match = archetype.includes(filter.Component);
          break;
        case 'oneOf':
          match = filter.Components.some((Comp) => archetype.includes(Comp));
      }
      if (!match) return;
    }

    this.archetypes.push(archetype);
    this.game.logger.debug(
      `Query ${this.toString()} added Archetype ${archetype.id}`,
    );
    this.unsubscribes.push(
      archetype.subscribe('entityRemoved', this.handleEntityRemoved),
    );
    this.unsubscribes.push(
      archetype.subscribe('entityAdded', this.handleEntityAdded),
    );
  };

  reset = () => {
    this.archetypes.length = 0;
    this.filter = [];
    this.unsubscribeArchetypes?.();
  };

  // closure provides iterator properties
  iterator = new QueryIterator<FilterDef>(this, this.game);

  [Symbol.iterator]() {
    return this.iterator;
  }

  first = () => {
    return this.iterator.first();
  };

  private handleEntityAdded = (entity: Entity) => {
    this.addToList(entity.id);
    this._generation++;
  };

  private handleEntityRemoved = (entityId: number) => {
    this.removeFromList(entityId);
    this._generation++;
  };

  toString() {
    return this.filter
      .map((filterItem) => {
        if (isFilter(filterItem)) {
          return filterItem.toString();
        }
        return (filterItem as any).name;
      })
      .join(',');
  }

  get archetypeIds() {
    return this.archetypes.map((a) => a.id);
  }

  get entities() {
    return this.trackedEntities as readonly number[];
  }

  get addedIds() {
    return this.addedThisFrame as readonly number[];
  }

  get added() {
    return this.addedIterable;
  }

  get removedIds() {
    return this.removedThisFrame as readonly number[];
  }

  get count() {
    return this.trackedEntities.length;
  }

  private addToList = (entityId: number) => {
    this.trackedEntities.push(entityId);
    const removedIndex = this.removedThisFrame.indexOf(entityId);
    if (removedIndex !== -1) {
      // this was a transfer (removes happen first)
      this.removedThisFrame.splice(removedIndex, 1);
      this.changesThisFrame--;
    } else {
      // only non-transfers count as adds
      this.addedThisFrame.push(entityId);
      this.changesThisFrame++;
    }
  };

  private removeFromList = (entityId: number) => {
    const index = this.trackedEntities.indexOf(entityId);
    if (index === -1) return;

    this.trackedEntities.splice(index, 1);
    this.removedThisFrame.push(entityId);
    this.changesThisFrame++;
  };

  private resetStepTracking = () => {
    this.addedThisFrame.length = 0;
    this.removedThisFrame.length = 0;
    this.changesThisFrame = 0;
  };

  private processAddRemove = () => {
    if (this.changesThisFrame) {
      this.addedThisFrame.forEach(this.emitAdded);
      this.removedThisFrame.forEach(this.emitRemoved);
    }
  };

  private emitAdded = (entityId: number) => {
    this.game.logger.debug(
      `Entity ${entityId} added to query ${this.toString()}`,
    );
    this.emit('entityAdded', entityId);
  };

  private emitRemoved = (entityId: number) => {
    this.game.logger.debug(
      `Entity ${entityId} removed from query ${this.toString()}`,
    );
    this.emit('entityRemoved', entityId);
  };

  destroy = () => {
    this.reset();
    this.unsubscribes.forEach((unsub) => unsub());
    this.unsubscribes.length = 0;
    this.emit('destroy');
  };
}

class AddedIterator<Def extends QueryComponentFilter>
  implements Iterator<EntityImpostorFor<Def>>
{
  private index = 0;
  private result: IteratorResult<EntityImpostorFor<Def>> = {
    done: true,
    value: null as any,
  };
  constructor(
    private game: Game,
    private query: Query<any>,
  ) {}

  next() {
    if (this.index >= this.query.addedIds.length) {
      this.result.done = true;
      return this.result;
    }
    this.result.done = false;
    this.result.value = this.game.get(this.query.addedIds[this.index]);
    return this.result;
  }
}
