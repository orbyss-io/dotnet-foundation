// Independent ECMAScript reference, with deterministic binary64 bit patterns.
let state = 0x51c0ffee;
function next() {
  state ^= state << 13; state ^= state >>> 17; state ^= state << 5;
  return state >>> 0;
}
const buffer = new ArrayBuffer(8), view = new DataView(buffer);
for (let i = 0; i < 10000; i++) {
  view.setUint32(0, next()); view.setUint32(4, next());
  const value = view.getFloat64(0);
  if (Number.isFinite(value) && value !== 0)
    console.log(JSON.stringify({ input: value.toExponential(17), expected: JSON.stringify(value) }));
}
for (const value of [{z:[2,1],a:"\u2028\u2029€\u0001\b\t\n\f\r\"\\"}, {"😀":1,"€":2,"a":3}]) {
  const ordered = Object.fromEntries(Object.keys(value).sort().map(key => [key,value[key]]));
  console.log(JSON.stringify({input:JSON.stringify(value), expected:JSON.stringify(ordered)}));
}
