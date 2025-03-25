export function mapValues<V, T extends Record<string, V>, U>(
  src: T,
  mapper: (v: V) => U
) {
  const mapped: Record<string, U> = {};
  let entry: [string, V];
  for (entry of Object.entries(src)) {
    mapped[entry[0]] = mapper(entry[1]);
  }
  return mapped;
}
