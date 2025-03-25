/**
 * IDs = 32-bit integers.
 * Lowest 24 bits are the ID itself.
 * Highest 8 bits are the version.
 * An ID with an incremented version is the same ID (same index),
 * but indicates that the resource referred to by an older version
 * is no longer existent.
 */

export const VERSION_MASK = 0b11111111000000000000000000000000;
export const SIGNIFIER_MASK = 0b00000000111111111111111111111111;

/**
 * Gets only the portion of the ID that signifes the
 * resource it represents.
 */
export function getIdSignifier(id: number) {
  // mask out high 8 bits
  return SIGNIFIER_MASK & id;
}

/**
 * Gets only the version portion of the ID
 */
export function getIdVersion(id: number) {
  return id >>> 24;
}

export function setIdVersion(id: number, version: number) {
  version = version % 255 << 24;
  return id | version;
}

export function incrementIdVersion(id: number) {
  return setIdVersion(id, getIdVersion(id) + 1);
}
