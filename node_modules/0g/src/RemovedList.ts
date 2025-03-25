import { Entity } from './Entity.js';

/**
 * Manages "removed" Entities, stuck in limbo between being
 * 'deleted' (from user perspective) and formally returned to
 * pools.
 */
export class RemovedList {
  private _list = new Array<Entity>();
  private _lookup = new Array<number>();

  add = (entity: Entity) => {
    this._lookup[entity.id] = this._list.length;
    this._list.push(entity);
  };

  flush = (callback: (entity: Entity) => void) => {
    while (this._list.length) {
      callback(this._list.shift()!);
    }
    this._lookup.length = 0;
  };

  get = (entityId: number) => {
    return this._list[this._lookup[entityId]] ?? null;
  };
}
