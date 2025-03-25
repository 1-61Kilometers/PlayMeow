export class ResourceHandle<T = any> {
  private _promise: Promise<T>;
  private _resolve: (value: T) => void = () => {
    throw new Error('Cannot resolve this resource yet');
  };
  private _value: T | null = null;

  __alive = false;

  constructor() {
    this._promise = new Promise<T>((resolve) => {
      this._resolve = resolve;
    });
  }

  resolve = (value: T) => {
    this._resolve(value);
    this._value = value;
  };

  get value() {
    return this._value;
  }

  get promise(): Promise<T> {
    return this._promise;
  }

  reset = () => {
    this._resolve = () => {
      throw new Error('Cannot resolve this resource yet');
    };
    this._promise = new Promise<T>((resolve) => {
      this._resolve = resolve;
    });
    this._value = null;
  };
}
