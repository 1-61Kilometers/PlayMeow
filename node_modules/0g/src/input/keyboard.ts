export type KeyboardKey = string;

export class Keyboard {
  private keysPressed = new Set<string>();
  private keysDown = new Set<string>();
  private keysUp = new Set<string>();

  private _blockBrowserShortcuts = false;

  set blockBrowserShortcuts(value: boolean) {
    this._blockBrowserShortcuts = value;
  }

  constructor() {
    window.addEventListener('keydown', this.handleKeyDown);
    window.addEventListener('keyup', this.handleKeyUp);
  }

  private handleKeyDown = (ev: KeyboardEvent) => {
    if (
      ev.target === document.body &&
      (this._blockBrowserShortcuts ||
        // allow F12
        (ev.key !== 'F12' &&
          // allow refresh shortcuts
          ev.key !== 'F5' &&
          !(ev.key === 'r' && (ev.ctrlKey || ev.metaKey))))
    ) {
      ev.preventDefault();
    }

    const key = ev.key;
    // avoid key-repeat triggering?
    if (!this.keysPressed.has(key)) {
      this.keysPressed.add(key);
      this.keysDown.add(key);
    }
  };

  private handleKeyUp = (ev: KeyboardEvent) => {
    const key = ev.key;
    this.keysPressed.delete(ev.key);
    this.keysUp.add(key);
  };

  getKeyPressed = (key: KeyboardKey) => {
    return this.keysPressed.has(key);
  };

  getKeyDown = (key: KeyboardKey) => {
    return this.keysDown.has(key);
  };

  getKeyUp = (key: KeyboardKey) => {
    return this.keysUp.has(key);
  };

  getAllPressedKeys = () => {
    return this.keysPressed;
  };

  frame = () => {
    this.keysDown.clear();
    this.keysUp.clear();
  };
}

export const keyboard = new Keyboard();
