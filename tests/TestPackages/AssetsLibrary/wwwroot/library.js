// A script of the component library. It is long enough to become smaller when it is compressed, so that the tests can
// check compression. Brotli and gzip add some bytes of their own, which only pay off for files of some size.
export const library = "library";
export function describe() { return "A script of the component library, served from the plugin's assets."; }
