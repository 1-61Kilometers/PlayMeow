import { Game } from './Game.js';
import { ObjectPool } from './internal/objectPool.js';
import { ComponentHandle, ComponentInstanceInternal } from './Component2.js';

export class ComponentPool {
  private pool: ObjectPool<ComponentInstanceInternal>;

  constructor(
    private handle: ComponentHandle,
    private game: Game,
  ) {
    this.pool = new ObjectPool<ComponentInstanceInternal>(
      () => this.handle.create(),
      (instance) => this.handle.reset(instance),
    );
  }

  acquire = (initial: any = {}, id: number) => {
    const instance = this.pool.acquire();
    this.handle.initialize(instance, initial, id);
    return instance;
  };

  release = (instance: ComponentInstanceInternal) => {
    this.pool.release(instance);
  };

  get ComponentType() {
    return this.handle;
  }

  destroy = () => {
    this.pool.destory();
  };
}
