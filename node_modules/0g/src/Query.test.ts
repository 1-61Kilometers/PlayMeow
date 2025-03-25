import { ArchetypeManager } from './ArchetypeManager.js';
import { Entity } from './Entity.js';
import { not, Not } from './filters.js';
import { Game } from './Game.js';
import { Query } from './Query.js';
import {
  ComponentA,
  ComponentB,
  ComponentC,
  ComponentD,
} from './__tests__/componentFixtures.js';
import { describe, it, beforeEach, expect, vi } from 'vitest';
import { EventSubscriber } from '@a-type/utils';
import { ComponentInstanceInternal } from './Component2.js';
import { defaultLogger } from './logger.js';

const withA = 100;
const withAB = 101;
const withB = 102;
const withC = 103;
const withD = 104;
const withAD = 105;

describe('Query', () => {
  let events: EventSubscriber<any>;
  let game: Game = null as any;

  // bootstrapping
  function addEntity(eid: number, components: ComponentInstanceInternal[]) {
    game.archetypeManager.createEntity(eid);
    components.forEach((comp) => {
      game.archetypeManager.addComponent(eid, comp);
    });
  }

  beforeEach(() => {
    events = new EventSubscriber();
    game = events as any;
    (game as any).componentManager = {
      count: 10,
      getTypeName: () => 'TEST MOCK',
    };
    (game as any).entityPool = {
      acquire() {
        return new Entity();
      },
      release() {},
    };
    (game as any).logger = defaultLogger;
    const archetypeManager = new ArchetypeManager(game);
    (game as any).archetypeManager = archetypeManager;

    // bootstrap some testing archetypes
    addEntity(withA, [ComponentA.create()]);
    addEntity(withB, [ComponentB.create()]);
    addEntity(withAB, [ComponentA.create(), ComponentB.create()]);
    addEntity(withC, [ComponentC.create()]);
    addEntity(withD, [ComponentD.create()]);
    addEntity(withAD, [ComponentA.create(), ComponentD.create()]);
  });

  it('registers Archetypes which match included components', () => {
    const query = new Query<[typeof ComponentA]>(game);
    query.initialize([ComponentA]);
    expect.assertions(4);
    expect(query.archetypeIds).toEqual([
      '01000000000',
      '01100000000',
      '01001000000',
    ]);
    for (const ent of query) {
      expect(ent.get(ComponentA)).not.toBe(null);
    }
  });

  it('registers Archetypes which omit excluded components', () => {
    const query = new Query<[typeof ComponentA, Not<typeof ComponentB>]>(game);
    query.initialize([ComponentA, not(ComponentB)]);
    expect.assertions(5);
    expect(query.archetypeIds).toEqual(['01000000000', '01001000000']);
    for (const ent of query) {
      expect(ent.get(ComponentA)).not.toBe(null);
      expect(ent.get(ComponentB)).toBe(null);
    }
  });

  it('registers late-added Archetypes', () => {
    const query = new Query<[typeof ComponentB]>(game);
    query.initialize([ComponentB]);
    addEntity(200, [ComponentB.create(), ComponentD.create()]);
    addEntity(201, [ComponentC.create(), ComponentD.create()]);
    expect.assertions(4);
    expect(query.archetypeIds).toEqual([
      '00100000000',
      '01100000000',
      '00101000000',
    ]);
    for (const ent of query) {
      expect(ent.get(ComponentB)).not.toBe(null);
    }
  });

  it('maintains a list of matching entities', () => {
    const onAdded = vi.fn();
    const onRemoved = vi.fn();

    const query = new Query<[typeof ComponentA]>(game);
    query.subscribe('entityAdded', onAdded);
    query.subscribe('entityRemoved', onRemoved);
    query.initialize([ComponentA]);
    expect(query.entities).toEqual([withA, withAB, withAD]);
    expect(query.addedIds).toEqual([withA, withAB, withAD]);
    expect(onAdded).toHaveBeenCalledTimes(3);

    // reset frame tracking
    events.emit('preApplyOperations');
    onAdded.mockClear();

    expect(query.entities).toEqual([withA, withAB, withAD]);
    expect(query.addedIds).toEqual([]);
    expect(query.removedIds).toEqual([]);

    // simple add case
    game.archetypeManager.addComponent(withC, ComponentA.create());
    events.emit('stepComplete');

    expect(query.entities).toEqual([withA, withAB, withAD, withC]);
    expect(query.addedIds).toEqual([withC]);
    expect(query.removedIds).toEqual([]);
    expect(onAdded).toHaveBeenCalledTimes(1);

    events.emit('preApplyOperations');
    onAdded.mockClear();

    expect(query.entities).toEqual([withA, withAB, withAD, withC]);
    expect(query.addedIds).toEqual([]);
    expect(query.removedIds).toEqual([]);

    // simple remove case
    game.archetypeManager.removeComponent(withAD, ComponentA.id);
    events.emit('stepComplete');

    expect(query.entities).toEqual([withA, withAB, withC]);
    expect(query.addedIds).toEqual([]);
    expect(query.removedIds).toEqual([withAD]);
    expect(onRemoved).toHaveBeenCalledTimes(1);

    events.emit('preApplyOperations');
    onAdded.mockClear();

    expect(query.entities).toEqual([withA, withAB, withC]);
    expect(query.addedIds).toEqual([]);
    expect(query.removedIds).toEqual([]);

    // internal move archetype case
    game.archetypeManager.addComponent(withA, ComponentC.create());
    events.emit('stepComplete');

    expect(query.entities).toEqual([withAB, withC, withA]);
    expect(query.addedIds).toEqual([]);
    expect(query.removedIds).toEqual([]);
    expect(onAdded).not.toHaveBeenCalled();
  });

  describe('events', () => {
    let query: Query<any>;
    const onAdded = vi.fn();
    const onRemoved = vi.fn();

    beforeEach(() => {
      query = new Query<[typeof ComponentA]>(game);
      query.initialize([ComponentA]);
      query.subscribe('entityAdded', onAdded);
      query.subscribe('entityRemoved', onRemoved);
      events.emit('preApplyOperations');
    });

    it('emits entityAdded events when an entity is added to matching Archetype', () => {
      addEntity(200, [ComponentA.create()]);
      addEntity(201, [ComponentA.create(), ComponentC.create()]);
      addEntity(202, [ComponentD.create()]);
      events.emit('stepComplete');
      expect(onAdded).toHaveBeenCalledTimes(2);
      expect(onAdded).toHaveBeenNthCalledWith(1, 200);
      expect(onAdded).toHaveBeenNthCalledWith(2, 201);
    });

    it('emits entityRemoved events when an entity is removed from matching Archetype', () => {
      game.archetypeManager.removeComponent(withAB, ComponentA.id);
      events.emit('stepComplete');
      expect(onRemoved).toHaveBeenCalledWith(withAB);
      expect(onAdded).not.toHaveBeenCalled();
    });

    it('does not emit added/removed when entity is moved between matching Archetypes', () => {
      game.archetypeManager.removeComponent(withAB, ComponentB.id);
      expect(onRemoved).not.toHaveBeenCalled();
      expect(onAdded).not.toHaveBeenCalled();
    });
  });
});
