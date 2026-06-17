import { openHelp } from '../config/help';

/**
 * Opens contextual TapeReplay help in the browser.
 * @param {{ page?: keyof import('../config/help').HELP_PAGES | 'index', children: import('react').ReactNode, className?: string }} props
 */
export default function HelpLink({ page = 'index', children, className = '' }) {
  return (
    <button
      type="button"
      className={`text-left text-sm text-sky-400 hover:text-sky-300 hover:underline ${className}`}
      onClick={() => openHelp(page)}
    >
      {children}
    </button>
  );
}
