export type LogLevel = 'info' | 'warn' | 'error' | 'debug';

export class Logger {
  static readonly levels: LogLevel[] = [
    'debug',
    'info',
    'warn',
    'error',
  ] as const;

  private lvl = 2;

  doLog = (level: LogLevel, ...messages: unknown[]) => {
    if (Logger.levels.indexOf(level) >= this.lvl) {
      console[level](...messages);
    }
  };

  constructor(level: LogLevel) {
    this.lvl = Logger.levels.indexOf(level);
  }

  info = this.doLog.bind(null, 'info');
  warn = this.doLog.bind(null, 'warn');
  error = this.doLog.bind(null, 'error');
  debug = this.doLog.bind(null, 'debug');
  log = this.doLog.bind(null, 'info');
}

export const defaultLogger = new Logger('info');
