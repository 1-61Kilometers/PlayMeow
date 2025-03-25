import { ComponentHandle } from './Component2.js';
import { Entity } from './Entity.js';
import { OneOf, Changed, Filter, not, Not } from './filters.js';
import { Game } from './Game.js';
import { Query, QueryComponentFilter } from './Query.js';

type FilterNots<CompUnion extends Filter<ComponentHandle> | ComponentHandle> =
  CompUnion extends Not<any> ? never : CompUnion;

type UnwrapAnys<CompUnion extends Filter<ComponentHandle> | ComponentHandle> =
  CompUnion extends OneOf<any> ? never : CompUnion;

type UnwrapFilters<
  CompUnion extends Filter<ComponentHandle> | ComponentHandle,
> = CompUnion extends Filter<infer C> ? C : CompUnion;

type DefiniteComponentsFromFilter<Fil extends QueryComponentFilter> =
  UnwrapFilters<UnwrapAnys<FilterNots<Fil[number]>>>;

export type EntityImpostorFor<Q extends QueryComponentFilter> = Entity<
  DefiniteComponentsFromFilter<Q>
>;

export class QueryIterator<Def extends QueryComponentFilter>
  implements Iterator<EntityImpostorFor<Def>>
{
  private archetypeIndex = 0;
  private archetypeIterator: Iterator<Entity<any>> | null = null;
  private result: IteratorResult<EntityImpostorFor<Def>> = {
    done: true,
    value: null as any,
  };
  private changedFilters: Changed<any>[];

  constructor(
    private query: Query<Def>,
    private game: Game,
  ) {
    this.changedFilters = query.filter.filter(
      (f) => f.kind === 'changed',
    ) as Changed<any>[];
  }

  private checkChangeFilter() {
    if (this.changedFilters.length === 0) return true;
    return this.changedFilters.some((filter) => {
      this.game.componentManager.wasChangedLastFrame(
        this.result.value.get(filter.Component).id,
      );
    });
  }

  next() {
    while (this.archetypeIndex < this.query.archetypes.length) {
      if (!this.archetypeIterator) {
        this.archetypeIterator =
          this.query.archetypes[this.archetypeIndex][Symbol.iterator]();
      }
      this.result = this.archetypeIterator.next();

      // if changed() filter(s) are present, ensure a change has
      // occurred in the specified components
      if (!this.result.done && !this.checkChangeFilter()) {
        continue;
      }

      // result is assigned from the current archetype iterator result -
      // if the archetype is done, we move on to the next archetype until
      // we run out
      if (this.result.done) {
        this.archetypeIndex++;
        this.archetypeIterator = null;
        continue;
      }
      return this.result;
    }

    this.result.done = true;
    this.archetypeIndex = 0;
    return this.result;
  }

  first() {
    this.archetypeIndex = 0;
    this.archetypeIterator = null;
    const first = this.next();
    // reset stateful bits
    this.archetypeIndex = 0;
    this.archetypeIterator = null;
    if (first.done) {
      return null;
    }
    return first.value;
  }
}
