import { describe, expect, it } from 'vitest';

import { milestoneOutlookLabel, severityLabel } from './delivery-labels';

describe('vocabulário de entrega', () => {
  it('a situação do marco tem rótulo e tom', () => {
    expect(milestoneOutlookLabel('behind')).toEqual({ label: 'Atrasado em relação ao prazo', tone: 'danger' });
  });

  it('a gravidade tem rótulo e tom', () => {
    expect(severityLabel('medium')).toEqual({ label: 'média', tone: 'warning' });
  });

  it('um valor que o motor passou a mandar aparece como veio, em tom neutro', () => {
    expect(milestoneOutlookLabel('estado-novo')).toEqual({ label: 'estado-novo', tone: 'neutral' });
    expect(severityLabel('critica')).toEqual({ label: 'critica', tone: 'neutral' });
  });

  it('nome herdado de objeto não é confundido com um valor conhecido', () => {
    expect(severityLabel('toString')).toEqual({ label: 'toString', tone: 'neutral' });
  });
});
