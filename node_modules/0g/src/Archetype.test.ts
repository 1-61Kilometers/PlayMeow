import { Archetype } from './Archetype.js';
import {
  ComponentA as A,
  ComponentB as B,
  ComponentC as C,
} from './__tests__/componentFixtures.js';
import { Entity } from './Entity.js';
import { describe, it, expect } from 'vitest';

describe('Archetypes', () => {
  const entities = [
    <const>[[A.create(), B.create(), C.create()] as const, 1],
    <const>[[A.create(), B.create(), C.create()] as const, 5],
    <const>[[A.create(), B.create(), C.create()] as const, 100],
  ];

  it('stores and iterates entities', () => {
    const arch = new Archetype<[typeof A, typeof B, typeof C]>('111');

    entities.forEach(([components, id]) => {
      const entity = new Entity();
      entity.__set(id, components);
      arch.addEntity(entity);
    });

    // ordering is not guaranteed on the iteration, so just storing in
    // an intermediate array
    let i = 0;
    for (const item of arch) {
      expect(item.id).toBe(entities[i][1]);
      expect(item.get(A)).toEqual(entities[i][0][0]);
      expect(item.get(B)).toEqual(entities[i][0][1]);
      expect(item.get(C)).toEqual(entities[i][0][2]);
      i++;
    }
  });

  it('removes entities', () => {
    const arch = new Archetype<[typeof A, typeof B, typeof C]>('111');

    entities.forEach(([components, id]) => {
      const entity = new Entity();
      entity.__set(id, components);
      arch.addEntity(entity);
    });

    arch.removeEntity(entities[1][1]);

    for (const item of arch) {
      expect(item.id).not.toEqual(5);
    }
  });

  it('keeps entity locations consistent after removal', () => {
    const arch = new Archetype<[typeof A, typeof B, typeof C]>('111');

    entities.forEach(([components, id]) => {
      const entity = new Entity();
      entity.__set(id, components);
      arch.addEntity(entity);
    });

    arch.removeEntity(entities[0][1]);

    expect(arch.getEntity(entities[1][1]).id).toEqual(entities[1][1]);
    expect(arch.getEntity(entities[2][1]).id).toEqual(entities[2][1]);
  });
});
