import { componentTypeIds } from './IdManager.js';

export const COMPONENT_CHANGE_HANDLE = Symbol('Component change handle');

export type BaseShape = Record<string, any>;

export type ComponentHandle<
  Shape extends BaseShape = any,
  Ext extends Extensions<Shape> = any,
> = {
  id: number;
  name: string;
  defaults: () => Shape;
  reset: (instance: Shape) => void;
  initialize: (
    pooled: ComponentInstanceInternal,
    initial: Partial<Shape>,
    id: number,
  ) => void;
  serialize?: Serializer;
  deserialize?: Deserializer;
  extensions?: Ext;
  create: () => ComponentInstance<Shape, Ext>;
  isInstance: (instance: any) => instance is ComponentInstance<Shape, Ext>;
};

type Serializer<Shape = any> = (shape: Shape) => string;
// TODO: should additionalproperties always be handled by the system not user-configurable?
type Deserializer<Shape = any> = (
  str: string,
  additionalProperties: PropertyDescriptorMap,
) => Shape;

type Extensions<Shape extends BaseShape> =
  | Record<string, (shape: ComponentInstance<Shape>) => any>
  | undefined;
// & {
//   // ban defining overlapping keys with Shape
//   [U in keyof Shape]?: never;
// };
type AppliedExtensions<Ex extends Extensions<any>> = Ex extends undefined
  ? {}
  : {
      [K in keyof Ex]: Ex[K] extends (...args: any[]) => any
        ? ReturnType<Ex[K]>
        : never;
    };

export type ComponentInstance$<
  Shape extends BaseShape = any,
  Ext extends Extensions<Shape> = Extensions<Shape>,
> = {
  /** A unique ID for this component instance */
  id: number;
  /** A reference to the component definition */
  type: ComponentHandle<Shape, Ext>;
  /** INTERNAL USE ONLY */
  [COMPONENT_CHANGE_HANDLE]?: (instance: ComponentInstance<Shape, Ext>) => void;
  /** Set changed = true to trigger changed() filters for this component */
  changed: boolean;
  /**
   * Call $() with a callback to automatically update the changed state for any
   * alterations made within the callback.
   */
  (callback: () => void): void;
};
export type ComponentInstance<
  Shape extends BaseShape = BaseShape,
  Ext extends Extensions<Shape> = Extensions<Shape>,
> = {
  $: ComponentInstance$<Shape, Ext>;
} & Shape &
  AppliedExtensions<Ext>;
// component just as seen by internal systems - no typing of
// data or extensions
export type ComponentInstanceInternal = {
  $: ComponentInstance$;
};

export type InstanceFor<Handle extends ComponentHandle> =
  Handle extends ComponentHandle<infer Shape, infer Ext>
    ? ComponentInstance<Shape, Ext>
    : never;

export type AnyComponent = ComponentInstanceInternal;

export type ComponentOptions<
  Shape extends BaseShape,
  Ext extends Extensions<Shape>,
> = {
  serialize?: Serializer<Shape>;
  deserialize?: Deserializer<Shape>;
  extensions?: Ext;
};

const defaultSerialize: Serializer = (instance) => {
  const gettersAndSetters: PropertyDescriptorMap = {};
  const data: Record<string, any> = {};
  const descriptors = Object.getOwnPropertyDescriptors(instance);
  Object.keys(descriptors).forEach((key) => {
    const descriptor = descriptors[key];
    if (
      typeof descriptor?.get === 'function' ||
      typeof descriptor?.set === 'function'
    ) {
      gettersAndSetters[key] = descriptor;
    } else if (
      descriptor.enumerable &&
      descriptor.value &&
      !(typeof descriptor.value === 'function')
    ) {
      data[key] = descriptor.value;
    }
  });
  return JSON.stringify(data);
};

const defaultDeserialize: Deserializer = (
  serialized: string,
  additionalProperties: PropertyDescriptorMap,
) => {
  const data = JSON.parse(serialized);
  Object.defineProperties(data, additionalProperties);
  return data as any;
};

export const componentTypeMap = new Map<number, ComponentHandle>();
const componentNameSet = new Set<string>();

function createComponentDefinition<
  Shape extends BaseShape,
  Ext extends Extensions<Shape>,
>(
  name: string,
  init: () => Shape,
  options?: ComponentOptions<Shape, Ext>,
): ComponentHandle<Shape, Ext> {
  if (componentNameSet.has(name)) {
    throw new Error(
      `Component name "${name}" already exists. Names must be unique. Use "namespace" to avoid conflicts.`,
    );
  }

  const handle = {
    id: componentTypeIds.get(),
    name,
    defaults: init,
  } as ComponentHandle<Shape, Ext>;
  componentTypeMap.set(handle.id, handle);

  function reset(instance: Shape) {
    Object.assign(instance, init());
  }
  function initialize(
    pooled: ComponentInstanceInternal,
    initial: Partial<Shape>,
    id: number,
  ) {
    Object.assign(pooled, init(), initial);
    const $ = ((callback: () => void) => {
      callback();
      pooled.$.changed = true;
    }) as ComponentInstance$;
    Object.defineProperties($, {
      id: { value: id, writable: false },
      type: { value: handle, writable: false },
      changed: {
        get() {
          console.warn('changed never returns true');
          return false;
        },
        set(_) {
          $[COMPONENT_CHANGE_HANDLE]?.(pooled);
        },
      },
    });
    pooled.$ = $;
  }
  function create(): ComponentInstance<Shape, Ext> {
    const instance: any = init();
    const extensions = options?.extensions;
    if (extensions) {
      Object.keys(extensions).forEach((key) => {
        Object.defineProperty(instance, key, {
          get: () => (extensions[key] as any)(instance),
        });
      });
    }
    initialize(instance, {}, 0);
    return instance;
  }
  function isInstance(
    instance: any,
  ): instance is ComponentInstance<Shape, Ext> {
    if (!('$' in instance)) return false;
    return instance.$.type.id === handle.id;
  }
  Object.assign(handle, {
    reset,
    create,
    initialize,
    isInstance,
    ...options,
  });
  return handle;
}

export function component<
  Shape extends BaseShape,
  Ext extends Extensions<Shape>,
>(name: string, init: () => Shape, options?: ComponentOptions<Shape, Ext>) {
  return createComponentDefinition(name, init, {
    serialize: defaultSerialize,
    deserialize: defaultDeserialize,
    ...options,
  });
}

export function state<Shape extends BaseShape, Ext extends Extensions<Shape>>(
  name: string,
  init: () => Shape,
  options?: Omit<ComponentOptions<Shape, Ext>, 'serialize' | 'deserialize'>,
) {
  return createComponentDefinition(name, init, options);
}

export function namespace(ns: string) {
  function namespacedComponent<
    Shape extends BaseShape,
    Ext extends Extensions<Shape>,
  >(name: string, init: () => Shape, options?: ComponentOptions<Shape, Ext>) {
    return component<Shape, Ext>(`${ns}:${name}`, init, options);
  }
  function namespacedState<
    Shape extends BaseShape,
    Ext extends Extensions<Shape>,
  >(
    name: string,
    init: () => Shape,
    options?: Omit<ComponentOptions<Shape, Ext>, 'serialize' | 'deserialize'>,
  ) {
    return state<Shape, Ext>(`${ns}:${name}`, init, options);
  }
  return {
    component: namespacedComponent,
    state: namespacedState,
  };
}
