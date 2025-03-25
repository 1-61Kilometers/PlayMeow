import { ComponentHandle } from './Component2.js';

export type Has<Comp extends ComponentHandle> = {
  Component: Comp;
  kind: 'has';
  __isFilter: true;
  toString(): string;
};

export const has = <Comp extends ComponentHandle>(
  Component: Comp,
): Has<Comp> => ({
  Component,
  kind: 'has',
  __isFilter: true,
  toString() {
    return `has(${Component.name})`;
  },
});

export type Not<Comp extends ComponentHandle> = {
  Component: Comp;
  kind: 'not';
  __isFilter: true;
  toString(): string;
};

export const not = <Comp extends ComponentHandle>(
  Component: Comp,
): Not<Comp> => ({
  Component,
  kind: 'not',
  __isFilter: true,
  toString() {
    return `not(${Component.name})`;
  },
});

export type Changed<Comp extends ComponentHandle> = {
  Component: Comp;
  kind: 'changed';
  __isFilter: true;
  toString(): string;
};

export const changed = <Comp extends ComponentHandle>(
  Component: Comp,
): Changed<Comp> => ({
  Component,
  kind: 'changed',
  __isFilter: true,
  toString() {
    return `changed(${Component.name})`;
  },
});

export type OneOf<Comps extends ComponentHandle[]> = {
  Components: Comps;
  kind: 'oneOf';
  __isFilter: true;
  toString(): string;
};

export const oneOf = <Comps extends ComponentHandle[]>(
  ...Components: Comps
): OneOf<Comps> => ({
  Components,
  kind: 'oneOf',
  __isFilter: true,
  toString() {
    return `oneOf(${Components.map((Comp) => Comp.name)
      .sort()
      .join(', ')})`;
  },
});
/** @deprecated - use oneOf */
export const any = oneOf;

export type Filter<Comp extends ComponentHandle> =
  | Not<Comp>
  | Has<Comp>
  | Changed<Comp>
  | OneOf<Comp[]>;

export const isFilter = (thing: any): thing is Filter<any> =>
  thing.__isFilter === true;

export const isNotFilter = (fil: Filter<any>): fil is Not<ComponentHandle> =>
  fil.kind === 'not';

export const isHasFilter = (fil: Filter<any>): fil is Has<ComponentHandle> =>
  fil.kind === 'has';

export const isChangedFilter = (
  fil: Filter<any>,
): fil is Changed<ComponentHandle> => fil.kind === 'changed';

export const isOneOfFilter = (
  fil: Filter<any>,
): fil is OneOf<ComponentHandle[]> => fil.kind === 'oneOf';
