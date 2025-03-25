import {
  ComponentHandle,
  ComponentInstance,
  ComponentInstanceInternal,
  InstanceFor,
} from './Component2.js';

// Utility type: it unwraps a ComponentDefinition to an instance, making it nullable
// if the type is not accounted for in the Entity definition, or "never" if the type
// is specifically omitted - otherwise it is non-nullable.
type DefinedInstance<
  Present extends ComponentHandle,
  Handle extends ComponentHandle,
> = Handle extends Present ? InstanceFor<Handle> : InstanceFor<Handle> | null;

export class Entity<
  DefiniteComponents extends ComponentHandle = ComponentHandle,
> {
  private _id = 0;
  // TODO: make array
  readonly components = new Map<number, ComponentInstance>();

  private _destroyed = true;
  private _removed = false;

  get id() {
    return this._id;
  }

  get destroyed() {
    return this._destroyed;
  }

  get removed() {
    return this._removed;
  }

  // TODO: hide these behind Symbols?
  __set = (
    entityId: number,
    components:
      | ComponentInstanceInternal[]
      | Readonly<ComponentInstanceInternal[]>,
  ) => {
    this._id = entityId;
    this.components.clear();
    components.forEach((comp) => {
      this.components.set(comp.$.type.id, comp);
    });
    this._destroyed = false;
    this._removed = false;
  };

  __addComponent = (instance: ComponentInstanceInternal) => {
    this.components.set(instance.$.type.id, instance);
  };

  __removeComponent = (typeId: number): ComponentInstanceInternal => {
    const instance = this.components.get(typeId);
    this.components.delete(typeId);
    return instance as ComponentInstanceInternal;
  };

  __markRemoved = () => {
    this._removed = true;
  };

  get = <T extends ComponentHandle>(
    handle: T,
  ): DefinedInstance<DefiniteComponents, T> => {
    const instance = (this.components.get(handle.id) ??
      null) as DefinedInstance<DefiniteComponents, T>;
    console.assert(
      !instance || instance.id !== 0,
      `Entity tried to access recycled Component instance of type ${handle.name}`,
    );
    return instance;
  };

  maybeGet = <T extends ComponentHandle>(handle: T): InstanceFor<T> | null => {
    return (this.components.get(handle.id) ?? null) as InstanceFor<T> | null;
  };

  has = <T extends ComponentHandle>(handle: T): boolean => {
    return this.components.has(handle.id);
  };

  reset() {
    this.components.clear();
    // disabled to diagnose issues...
    this._id = 0;
    this._destroyed = true;
  }

  clone(other: Entity<any>) {
    other.components.forEach((value, key) => {
      this.components.set(key, value);
    });
    this._id = other.id;
  }
}
