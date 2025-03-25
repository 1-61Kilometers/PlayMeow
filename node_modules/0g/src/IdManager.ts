import { incrementIdVersion, setIdVersion, SIGNIFIER_MASK } from './ids.js';

/**
 * Provides monotonically increasing ID numbers. Allows
 * releasing unused IDs back to pool.
 */
export class IdManager {
  private recycled = new Array<number>();
  private active = new Array<number>();
  private allocatedCount = 0;

  constructor(private log?: (...msg: any[]) => void) {}

  get() {
    let id: number | undefined;
    id = this.recycled.shift();
    if (!id) {
      if (this.allocatedCount >= SIGNIFIER_MASK) {
        throw new Error('Ran out of IDs');
      }
      // incrementing first means ids start at 1
      id = ++this.allocatedCount;
      id = setIdVersion(id, 0);

      this.log?.('New ID allocated, total:', this.allocatedCount);
    }
    this.active.push(id);
    return id!;
  }

  release(id: number) {
    const index = this.active.indexOf(id);
    if (index === -1) {
      throw new Error(`Tried to release inactive ID ${id}`);
    }
    this.active.splice(index, 1);
    this.recycled.push(incrementIdVersion(id));
  }
}

// global, since component() and state() use it
export const componentTypeIds = new IdManager();
