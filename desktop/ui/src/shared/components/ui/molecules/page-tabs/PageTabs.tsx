import { createContext, useContext } from 'react';
import { NavLink } from 'react-router';
import styles from './PageTabs.module.scss';

export interface PageTab {
  label: string;
  to: string;
}

export interface PageTabSet {
  /** O nome do item do menu que as abas dividem; vira o nome acessível da navegação. */
  label: string;
  tabs: readonly PageTab[];
}

const PageTabsContext = createContext<PageTabSet | null>(null);

/** O shell diz quais telas dividem o item aberto; a página só desenha. */
export const PageTabsProvider = PageTabsContext.Provider;

export function usePageTabs(): PageTabSet | null {
  return useContext(PageTabsContext);
}

/**
 * Links, não `role="tab"`: cada aba é uma tela com endereço próprio, então o padrão de navegação (com `aria-current`) é o
 * que o leitor de tela anuncia certo, e voltar no histórico funciona.
 */
export function PageTabs({ set }: { set: PageTabSet | null }) {
  if (!set || set.tabs.length < 2) return null;

  return (
    <nav aria-label={`Seções de ${set.label}`} className={styles.tabs}>
      <ul className={styles.list}>
        {set.tabs.map((tab) => (
          <li key={tab.to}>
            <NavLink to={tab.to} end className={styles.tab}>
              {tab.label}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
