export class ObjectPool<T> {
  private free = new Array<T>();
  private count = 0;

  constructor(
    private factory: () => T,
    private reset: (item: T) => void = () => {},
    initialSize: number = 1,
  ) {
    this.expand(initialSize);
  }

  acquire() {
    // Grow the list by 20%ish if we're out
    if (this.free.length <= 0) {
      this.expand(Math.round(this.count * 0.2) + 1);
    }

    var item = this.free.pop()!;

    return item;
  }

  release(item: T) {
    if (!item) {
      console.warn(`Tried to release ${item}. This might be a bug.`);
      return;
    }
    this.reset(item);
    this.free.push(item);
  }

  expand(count: number) {
    for (var n = 0; n < count; n++) {
      var clone = this.factory();
      this.free.push(clone);
    }
    this.count += count;
  }

  get size() {
    return this.count;
  }

  get freeCount() {
    return this.free.length;
  }

  get usedCount() {
    return this.count - this.free.length;
  }

  destory = () => {
    this.free.length = 0;
  };
}
