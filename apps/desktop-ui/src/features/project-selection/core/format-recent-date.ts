const time = new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' });
const date = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' });

const startOfDay = (ms: number) => {
  const d = new Date(ms);
  d.setHours(0, 0, 0, 0);
  return d.getTime();
};

/** "Hoje · 14:32", "Ontem · 16:20" ou "23/10/2025 · 10:15". `now` existe para o teste fixar o relógio. */
export function formatRecentDate(ms: number, now: number = Date.now()): string {
  const days = Math.round((startOfDay(now) - startOfDay(ms)) / 86_400_000);
  const day = days === 0 ? 'Hoje' : days === 1 ? 'Ontem' : date.format(ms);
  return `${day} · ${time.format(ms)}`;
}
