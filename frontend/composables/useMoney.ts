/** 295000 → "295 000 ₴". Plain string ops, so server and browser agree byte for byte. */
export const useMoney = () => (n: number): string =>
  `${Math.round(n).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ' ')} ₴`
