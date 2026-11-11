const day = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', timeZone: 'UTC' });
const decimal = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1 });

/** "03/11 a 10/11": a janela de 7 dias como a pessoa lê. O fim é exclusivo no motor; aqui mostra o último dia contado. */
export function formatWindow(from: string, to: string): string {
  const end = Date.parse(to) - 1;
  return `${day.format(Date.parse(from))} a ${day.format(end)}`;
}

/** "1,5": vírgula decimal e no máximo uma casa, igual ao arredondamento do motor. */
export function formatDecimal(value: number): string {
  return decimal.format(value);
}

/** "2,5 dias", "1 dia". */
export function formatDays(value: number): string {
  return `${formatDecimal(value)} ${value === 1 ? 'dia' : 'dias'}`;
}
