import { EventSubscriber } from '@a-type/utils';
import { ComponentHandle } from './Component2.js';
import { Entity } from './Entity.js';
import { getIdSignifier } from './ids.js';

export type ArchetypeEvents = {
  entityAdded(entity: Entity<any>): any;
  entityRemoved(entityId: number): any;
};

/**
 * Archetype is a group of Entities which share a common component signature.
 * Archetypes are the storage system for Entities; each Entity traces back to an Archetype's
 * entities array. When Entity components change, they are moved from Archetype to Archetype.
 * Grouping in this way is a helpful shortcut to fulfilling Query filter requirements,
 * as we only need to map a small number of Archetypes -> Query, versus iterating over
 * and checking every Entity in the system at init and then on every change.
 */
export class Archetype<
  T extends ComponentHandle[] = ComponentHandle[],
> extends EventSubscriber<ArchetypeEvents> {
  private entities = new Array<Entity<T[number]>>();
  /** Maps entity ID -> index in entity array */
  private entityIndexLookup = new Array<number | undefined>();

  constructor(public id: string) {
    super();
  }

  /**
   * Archetype is iterable; iterating it will iterate over its stored
   * Entities.
   *
   * TODO: reverse?
   */
  [Symbol.iterator]() {
    return this.entities[Symbol.iterator]();
  }

  private setLookup(entityId: number, index: number) {
    this.entityIndexLookup[getIdSignifier(entityId)] = index;
  }

  private getLookup(entityId: number) {
    return this.entityIndexLookup[getIdSignifier(entityId)];
  }

  private clearLookup(entityId: number) {
    this.entityIndexLookup[getIdSignifier(entityId)] = undefined;
  }

  addEntity(entity: Entity<any>) {
    // this is the index ("column") of this entity in the table
    const index = this.entities.length;
    // for lookup later when presented with an entityId
    this.setLookup(entity.id, index);

    // add entity data to the column of all data arrays
    this.entities[index] = entity;
    this.emit('entityAdded', entity);
  }

  /**
   * Removes an entity from the archetype table, returning its
   * component data list
   */
  removeEntity(entityId: number) {
    const index = this.getLookup(entityId);
    if (index === undefined) {
      throw new Error(
        `Tried to remove ${entityId} from archetype ${this.id}, but was not present`,
      );
    }
    this.clearLookup(entityId);

    const [entity] = this.entities.splice(index, 1);
    // FIXME: improve this!!! Maybe look into a linked list like that one blog post...
    // decrement all entity index lookups that fall after this index
    for (let i = 0; i < this.entityIndexLookup.length; i++) {
      if (this.entityIndexLookup[i] && this.entityIndexLookup[i]! > index) {
        this.entityIndexLookup[i]!--;
      }
    }

    this.emit('entityRemoved', entityId);
    return entity;
  }

  getEntity(entityId: number) {
    const index = this.getLookup(entityId);
    if (index === undefined) {
      throw new Error(
        `Could not find entity ${entityId} in archetype ${this.id}`,
      );
    }
    return this.entities[index];
  }

  hasAll = (types: ComponentHandle[]) => {
    const masked = types
      .reduce((m, T) => {
        m[T.id] = '1';
        return m;
      }, this.id.split(''))
      .join('');
    return this.id === masked;
  };

  hasSome = (types: ComponentHandle[]) => {
    for (var T of types) {
      if (this.id[T.id] === '1') return true;
    }
    return false;
  };

  includes = (Type: ComponentHandle) => {
    return this.id[Type.id] === '1';
  };

  omits = (Type: ComponentHandle) => {
    return !this.includes(Type);
  };

  toString() {
    return this.id;
  }
}
