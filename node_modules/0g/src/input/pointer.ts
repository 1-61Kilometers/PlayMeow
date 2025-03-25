export class Pointer {
  private _position: { x: number; y: number } | null = null;
  private _primaryPressed = false;
  private _primaryDown = false;
  private _primaryUp = false;
  private _secondaryPressed = false;
  private _secondaryDown = false;
  private _secondaryUp = false;

  constructor() {
    window.addEventListener('pointerdown', this.handlePointerDownEvents);
    window.addEventListener('pointerup', this.handlePointerDownEvents);
    window.addEventListener('pointermove', this.handlePointerMoveEvent);
  }

  private handlePointerDownEvents = (ev: PointerEvent) => {
    if (!this._position) {
      this._position = { x: ev.clientX, y: ev.clientY };
    } else {
      this._position.x = ev.clientX;
      this._position.y = ev.clientY;
    }

    if (ev.type === 'pointerdown') {
      if (ev.button === 0) {
        this._primaryDown = true;
        this._primaryPressed = true;
      } else if (ev.button === 2) {
        this._secondaryDown = true;
        this._secondaryPressed = true;
      }
    } else if (ev.type === 'pointerup') {
      if (ev.button === 0) {
        this._primaryUp = true;
        this._primaryPressed = false;
      } else if (ev.button === 2) {
        this._secondaryUp = true;
        this._secondaryPressed = false;
      }
    }
  };

  private handlePointerMoveEvent = (ev: PointerEvent) => {
    this._position = { x: ev.clientX, y: ev.clientY };
  };

  /** Position might be null - that means no pointer was detected */
  get position() {
    return this._position;
  }

  get primaryPressed() {
    return this._primaryPressed;
  }

  get primaryDown() {
    return this._primaryDown;
  }

  get primaryUp() {
    return this._primaryUp;
  }

  get secondaryPressed() {
    return this._secondaryPressed;
  }

  get secondaryDown() {
    return this._secondaryDown;
  }

  get secondaryUp() {
    return this._secondaryUp;
  }

  frame = () => {
    this._primaryDown = false;
    this._primaryUp = false;
    this._secondaryDown = false;
    this._secondaryUp = false;
  };
}

export const pointer = new Pointer();
