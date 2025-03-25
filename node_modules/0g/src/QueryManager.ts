import { isFilter } from './filters.js';
import { Game } from './Game.js';
import { Query, QueryComponentFilter } from './Query.js';

export class QueryManager {
  private queryCache: Map<string, Query<any>> = new Map();

  constructor(private game: Game) {}

  private getQueryKey(def: QueryComponentFilter) {
    return def
      .map((filter) =>
        isFilter(filter) ? filter.toString() : `has(${filter.name})`,
      )
      .join(',');
  }

  private handleQueryCreated = (query: Query<any>) => {
    const unsub = query.subscribe('destroy', () => {
      this.release(query);
      unsub();
    });
  };

  create<Def extends QueryComponentFilter>(userDef: Def) {
    const key = this.getQueryKey(userDef);
    if (this.queryCache.has(key)) {
      return this.queryCache.get(key) as Query<Def>;
    }
    const query = new Query<Def>(this.game);
    query.reset();
    query.initialize(userDef);
    this.queryCache.set(key, query);
    this.handleQueryCreated(query);
    return query as Query<Def>;
  }

  release(query: Query<any>) {
    this.queryCache.delete(this.getQueryKey(query.filter));
  }
}
