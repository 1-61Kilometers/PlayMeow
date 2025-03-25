import { QueryManager } from './QueryManager.js';
import { ComponentManager } from './ComponentManager.js';
import { IdManager } from './IdManager.js';
import { ArchetypeManager } from './ArchetypeManager.js';
import { Operation, OperationQueue } from './operations.js';
import { Entity } from './Entity.js';
import { Resources } from './Resources.js';
import { ObjectPool } from './internal/objectPool.js';
import { RemovedList } from './RemovedList.js';
import { AssetLoaderImpls, Assets } from './Assets.js';
import { QueryComponentFilter } from './Query.js';
import { EntityImpostorFor } from './QueryIterator.js';
import {
  type AssetLoaders,
  type BaseShape,
  type ComponentInstanceInternal,
  type Globals,
} from './index.js';
import { EventSubscriber } from '@a-type/utils';
import { ComponentHandle } from './Component2.js';
import { allSystems } from './System.js';
import { Logger } from './logger.js';

export type GameConstants = {
  maxComponentId: number;
  maxEntities: number;
};

export type GameEvents = {
  [phase: `phase:${string}`]: any;
  stepComplete(): any;
  preApplyOperations(): any;
  destroyEntities(): any;
};

export class Game {
  private events = new EventSubscriber<GameEvents>();
  private _queryManager: QueryManager;
  private _entityIds = new IdManager((...msgs) =>
    console.debug('Entity IDs:', ...msgs),
  );
  private _archetypeManager: ArchetypeManager;
  // operations applied every step
  private _stepOperationQueue: OperationQueue = [];
  // operations applied every phase
  private _phaseOperationQueue: OperationQueue = [];
  private _componentManager: ComponentManager;
  private _globals;
  private _runnableCleanups: (() => void)[];
  private _entityPool = new ObjectPool(
    () => new Entity(),
    (e) => e.reset(),
  );
  private _removedList = new RemovedList();
  private _assets: Assets<AssetLoaders>;

  private _phases = ['preStep', 'step', 'postStep'];

  private _delta = 0;
  private _time = 0;

  private _constants: GameConstants = {
    maxComponentId: 256,
    maxEntities: 2 ** 16,
  };

  readonly logger;

  constructor({
    assetLoaders = {},
    ignoreSystemsWarning,
    phases,
    logLevel,
  }: {
    assetLoaders?: AssetLoaderImpls<AssetLoaders>;
    ignoreSystemsWarning?: boolean;
    phases?: string[];
    logLevel?: 'debug' | 'info' | 'warn' | 'error';
  } = {}) {
    this._phases = phases ?? this._phases;
    this._componentManager = new ComponentManager(this);
    this._assets = new Assets(assetLoaders);
    this._queryManager = new QueryManager(this);
    this._archetypeManager = new ArchetypeManager(this);
    this._globals = new Resources<Globals>(this);
    this.logger = new Logger(logLevel ?? 'info');

    if (allSystems.length === 0 && !ignoreSystemsWarning) {
      throw new Error(
        'No systems are defined at the type of game construction. You have to define systems before calling the Game constructor. Did you forget to import modules which define your systems?',
      );
    }
    this._runnableCleanups = allSystems
      .map((sys) => sys(this))
      .filter(Boolean) as (() => void)[];
    console.debug(`Registered ${allSystems.length} systems`);
  }

  get entityIds() {
    return this._entityIds;
  }
  get componentManager() {
    return this._componentManager;
  }
  get archetypeManager() {
    return this._archetypeManager;
  }
  get delta() {
    return this._delta;
  }
  get time() {
    return this._time;
  }
  get queryManager() {
    return this._queryManager;
  }
  get constants() {
    return this._constants;
  }
  get globals() {
    return this._globals;
  }
  get assets() {
    return this._assets;
  }
  get entityPool() {
    return this._entityPool;
  }

  subscribe = <K extends keyof GameEvents>(
    event: K,
    listener: GameEvents[K],
  ) => {
    if (event.startsWith('phase:') && !this._phases.includes(event.slice(6))) {
      throw new Error(
        `Unknown phase: ${event.slice(6)}. Known phases: ${this._phases.join(', ')}. Add this phase to your phases array in the Game constructor if you want to use it.`,
      );
    }
    return this.events.subscribe(event, listener);
  };

  /**
   * Allocates a new entity id and enqueues an operation to create the entity at the next opportunity.
   */
  create = () => {
    const id = this.entityIds.get();
    this.enqueueStepOperation({
      op: 'createEntity',
      entityId: id,
    });
    return id;
  };

  /**
   * Enqueues an entity to be destroyed at the next opportunity
   */
  destroy = (id: number) => {
    this.enqueueStepOperation({
      op: 'removeEntity',
      entityId: id,
    });
  };

  /**
   * Add a component to an entity.
   */
  add = <ComponentShape extends BaseShape>(
    entity: number | Entity,
    handle: ComponentHandle<ComponentShape>,
    initial?: Partial<ComponentShape>,
  ) => {
    const entityId = typeof entity === 'number' ? entity : entity.id;
    this.enqueueStepOperation({
      op: 'addComponent',
      entityId,
      componentType: handle.id,
      initialValues: initial,
    });
  };

  /**
   * Remove a component by type from an entity
   */
  remove = <T extends ComponentHandle, E extends Entity<any>>(
    entity: number | E,
    Type: T,
  ) => {
    if (!(typeof entity === 'number')) {
      // ignore removed entities, their components
      // are already gone.
      if (entity.removed) {
        return;
      }
      // TODO: find a way to do this when the
      // arg isn't an entity
    }
    const entityId = typeof entity === 'number' ? entity : entity.id;
    this.enqueueStepOperation({
      op: 'removeComponent',
      entityId,
      componentType: Type.id,
    });
  };

  /**
   * Get a single entity by its known ID
   */
  get = (entityId: number): Entity<any> | null => {
    return (
      this.archetypeManager.getEntity(entityId) ??
      this._removedList.get(entityId)
    );
  };

  /**
   * Run some logic for each entity that meets an ad-hoc query.
   */
  query = <Filter extends QueryComponentFilter>(
    filter: Filter,
    run: (entity: EntityImpostorFor<Filter>, game: this) => void,
  ): void => {
    const query = this._queryManager.create(filter);
    let ent;
    for (ent of query) {
      run(ent, this);
    }
  };

  find = <Filter extends QueryComponentFilter>(
    filter: Filter,
  ): EntityImpostorFor<Filter>[] => {
    const query = this._queryManager.create(filter);
    return Array.from(query);
  };

  findFirst = <Filter extends QueryComponentFilter>(
    filter: Filter,
  ): EntityImpostorFor<Filter> | null => {
    const query = this._queryManager.create(filter);
    return query.first();
  };

  /**
   * Manually step the game simulation forward. Provide a
   * delta (in ms) of time elapsed since last frame.
   */
  step = (delta: number) => {
    this._delta = delta;
    for (const phase of this._phases) {
      this.events.emit(`phase:${phase}`);
      this.flushPhaseOperations();
    }
    this.events.emit('destroyEntities');
    this._removedList.flush(this.destroyEntity);
    this.events.emit('preApplyOperations');
    this.flushStepOperations();
    this.events.emit('stepComplete');
  };

  enqueuePhaseOperation = (operation: Operation) => {
    this._phaseOperationQueue.push(operation);
  };

  enqueueStepOperation = (operation: Operation) => {
    this._stepOperationQueue.push(operation);
  };

  // entities aren't actually destroyed until the end of the following
  //step when this is called. It gives effects time to react to the
  // removal of the entity.
  private destroyEntity = (entity: Entity) => {
    entity.components.forEach((instance) => {
      if (instance) this.componentManager.release(instance);
    });
    const id = entity.id;
    this.entityIds.release(id);
    this.entityPool.release(entity);
    this.logger.debug('Destroyed entity', id);
  };

  private flushPhaseOperations = () => {
    while (this._phaseOperationQueue.length) {
      this.applyOperation(this._phaseOperationQueue.shift()!);
    }
  };

  private flushStepOperations = () => {
    while (this._stepOperationQueue.length) {
      this.applyOperation(this._stepOperationQueue.shift()!);
    }
  };

  private applyOperation = (operation: Operation) => {
    let instance: ComponentInstanceInternal | undefined;
    let entity: Entity;

    switch (operation.op) {
      case 'addComponent':
        if (operation.entityId === 0) break;

        instance = this.componentManager.acquire(
          operation.componentType,
          operation.initialValues,
        );
        this.archetypeManager.addComponent(operation.entityId, instance);
        break;
      case 'removeComponent':
        if (operation.entityId === 0) break;

        this.logger.debug(
          'Removing component',
          operation.componentType,
          'from entity',
          operation.entityId,
        );

        // if entity was removed already, it won't
        // be in archetypes anymore, and the component
        // will be released in the removal process.
        if (this._removedList.get(operation.entityId)) break;

        instance = this.archetypeManager.removeComponent(
          operation.entityId,
          operation.componentType,
        );
        if (instance) {
          this.componentManager.release(instance);
        }
        break;
      case 'createEntity':
        this.archetypeManager.createEntity(operation.entityId);
        break;
      // removal is not destruction - the entity object will remain
      // allocated with components, but removed from archetypes
      // and therefore queries. effects get one last look at it
      // before it is returned to the pool.
      case 'removeEntity':
        if (operation.entityId === 0) break;

        entity = this.archetypeManager.removeEntity(operation.entityId);

        this._removedList.add(entity);
        break;
      case 'markChanged':
        this.componentManager.markChanged(operation.componentId);
        break;
    }
  };
}
