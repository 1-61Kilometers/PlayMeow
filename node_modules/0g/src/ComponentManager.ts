import { ComponentPool } from './ComponentPool.js';
import {
  COMPONENT_CHANGE_HANDLE,
  ComponentInstance,
  ComponentInstanceInternal,
  componentTypeMap,
} from './Component2.js';
import { Game } from './Game.js';
import { IdManager } from './IdManager.js';

/**
 * Manages pools of Components based on their Type, and
 * the presence of Components assigned to Entities.
 */
export class ComponentManager {
  private pools = new Array<ComponentPool>();
  private changed = new Array<boolean>();
  private unsubscribes = new Array<() => void>();
  private componentIds = new IdManager();

  constructor(private game: Game) {
    // pre-allocate pools for each ComponentType
    for (const [id, val] of componentTypeMap) {
      this.pools[id] = new ComponentPool(val, this.game);
    }

    // TODO: right time to do this?
    this.unsubscribes.push(
      game.subscribe('preApplyOperations', this.resetChanged),
    );
  }

  public get count() {
    return componentTypeMap.size;
  }

  acquire = (typeId: number, initialValues: any) => {
    if (!this.pools[typeId]) {
      throw new Error(`ComponentType with ID ${typeId} does not exist`);
    }
    const component = this.pools[typeId].acquire(
      initialValues,
      this.componentIds.get(),
    );
    component.$[COMPONENT_CHANGE_HANDLE] = this.onComponentChanged;
    return component;
  };

  release = (instance: ComponentInstanceInternal) => {
    delete instance.$[COMPONENT_CHANGE_HANDLE];
    this.componentIds.release(instance.$.id);
    return this.pools[instance.$.type.id].release(instance);
  };

  wasChangedLastFrame = (componentInstanceId: number) => {
    return !!this.changed[componentInstanceId];
  };

  private onComponentChanged = (component: ComponentInstanceInternal) => {
    this.game.enqueueStepOperation({
      op: 'markChanged',
      componentId: component.$.id,
    });
  };

  markChanged = (componentId: number) => {
    this.changed[componentId] = true;
  };

  private resetChanged = () => {
    this.changed.length = 0;
  };

  getTypeName = (typeId: number) => {
    return this.pools[typeId].ComponentType.name;
  };

  destroy = () => {
    this.unsubscribes.forEach((unsub) => unsub());
    this.pools.forEach((pool) => pool.destroy());
  };
}
