import { ObjectPool } from './internal/objectPool.js';
import { ResourceHandle } from './ResourceHandle.js';

export type AssetLoader<T = any> = (key: string) => Promise<T>;
export type AssetLoaderImpls<Assets extends Record<string, any>> = {
  [key in keyof Assets]: AssetLoader<Assets[key]>;
};

export class Assets<Loaders extends Record<string, any>> {
  private handlePool = new ObjectPool(
    () => new ResourceHandle(),
    (h) => h.reset(),
  );
  private handles = new Map<string, ResourceHandle>();

  constructor(private _loaders: AssetLoaderImpls<Loaders>) {}

  load = <LoaderName extends keyof Loaders | (string & {})>(
    loader: LoaderName,
    key: string,
  ) => {
    let handle = this.handles.get(this.getKey(loader, key));
    if (!handle) {
      handle = this.handlePool.acquire();
      this.handles.set(this.getKey(loader, key), handle);
      this._loaders[loader](key).then((value: AssetLoader) =>
        handle!.resolve(value),
      );
    }
    return handle!.promise as Promise<Loaders[LoaderName]>;
  };

  immediate = <LoaderName extends keyof Loaders | (string & {})>(
    loader: LoaderName,
    key: string,
  ) => {
    const handle = this.handles.get(this.getKey(loader, key));
    if (!handle) return null;
    return handle.value as Loaders[LoaderName] | null;
  };

  private getKey = (loader: keyof Loaders | (string & {}), key: string) =>
    `${loader.toString()}:${key}`;
}
