import { forwardRef, type AnchorHTMLAttributes } from 'react';
import { Link } from 'react-router';

/** Liga os `href` dos componentes do Synthra (Breadcrumb, Link, Button com `href`) ao roteador, sem recarregar a página. */
export const RouterLinkBehavior = forwardRef<
  HTMLAnchorElement,
  AnchorHTMLAttributes<HTMLAnchorElement> & { href: string }
>(function RouterLinkBehavior({ href, ...props }, ref) {
  return <Link ref={ref} to={href} {...props} />;
});
