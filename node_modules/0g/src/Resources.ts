import { Game } from './index.js';
import { ObjectPool } from './internal/objectPool.js';
import { ResourceHandle } from './ResourceHandle.js';

export class Resources<ResourceMap extends Record<string, any>> {
  private handlePool = new ObjectPool(
    () => new ResourceHandle(),
    (h) => h.reset(),
  );
  private handles = new Map<string | number | symbol, ResourceHandle>();

  constructor(private game: Game) {}

  private getOrCreateGlobalHandle = (key: string | number | symbol) => {
    let handle = this.handles.get(key);
    if (!handle) {
      handle = this.handlePool.acquire();
      this.handles.set(key, handle);
    }
    return handle;
  };

  load = <Key extends keyof ResourceMap | (string & {})>(key: Key) => {
    return this.getOrCreateGlobalHandle(key).promise as Promise<
      Key extends keyof ResourceMap ? ResourceMap[Key] : any
    >;
  };

  resolve = <Key extends keyof ResourceMap | (string & {})>(
    key: Key,
    value: Key extends keyof ResourceMap ? ResourceMap[Key] : any,
  ) => {
    this.getOrCreateGlobalHandle(key).resolve(value);
    this.game.logger.debug('Resolved resource', key);
  };

  immediate = <Key extends keyof ResourceMap | (string & {})>(key: Key) => {
    return this.getOrCreateGlobalHandle(key).value as
      | (Key extends keyof ResourceMap ? ResourceMap[Key] : any)
      | null;
  };

  remove = (key: keyof ResourceMap | (string & {})) => {
    const value = this.handles.get(key);
    if (value) {
      this.handlePool.release(value);
      this.handles.delete(key);
    }
  };
}
