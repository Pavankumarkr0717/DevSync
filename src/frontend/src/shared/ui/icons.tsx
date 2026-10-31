import type { ReactNode, SVGProps } from 'react';

// Inline 24×24 stroke icons in the style of the reference mockups (docs/design/design-system.md).
// Decorative by default: pass `title` to give an icon an accessible name.

export type IconProps = SVGProps<SVGSVGElement> & { size?: number; title?: string };

function icon(paths: ReactNode) {
  return function Icon({ size = 20, title, ...rest }: IconProps) {
    return (
      <svg
        width={size}
        height={size}
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth={1.75}
        strokeLinecap="round"
        strokeLinejoin="round"
        aria-hidden={title ? undefined : true}
        role={title ? 'img' : undefined}
        focusable="false"
        {...rest}
      >
        {title && <title>{title}</title>}
        {paths}
      </svg>
    );
  };
}

export const LogoMark = ({ size = 32 }: { size?: number }) => (
  <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true" focusable="false">
    <rect width="32" height="32" rx="8" fill="var(--accent)" />
    <rect x="8" y="8" width="10" height="10" rx="2" fill="none" stroke="#fff" strokeWidth="2" />
    <rect x="14" y="14" width="10" height="10" rx="2" fill="none" stroke="#fff" strokeWidth="2" />
  </svg>
);

export const HomeIcon = icon(<><path d="M3 10.5 12 3l9 7.5" /><path d="M5 9.5V21h14V9.5" /><path d="M10 21v-6h4v6" /></>);
export const MailIcon = icon(<><rect x="3" y="5" width="18" height="14" rx="2" /><path d="m3 7 9 6 9-6" /></>);
export const UserIcon = icon(<><circle cx="12" cy="8" r="4" /><path d="M4 21a8 8 0 0 1 16 0" /></>);
export const UsersIcon = icon(<><circle cx="9" cy="8" r="3.5" /><path d="M2.5 20a6.5 6.5 0 0 1 13 0" /><path d="M16 4.5a3.5 3.5 0 0 1 0 7" /><path d="M18 14a6.5 6.5 0 0 1 3.5 6" /></>);
export const LogoutIcon = icon(<><path d="M15 4h4v16h-4" /><path d="M10 8l-4 4 4 4" /><path d="M6 12h10" /></>);
export const PlusIcon = icon(<><path d="M12 5v14" /><path d="M5 12h14" /></>);
export const MinusIcon = icon(<path d="M5 12h14" />);
export const ChevronRightIcon = icon(<path d="m9 6 6 6-6 6" />);
export const ChevronDownIcon = icon(<path d="m6 9 6 6 6-6" />);
export const ArrowLeftIcon = icon(<><path d="M19 12H5" /><path d="m11 18-6-6 6-6" /></>);
export const ArrowRightIcon = icon(<><path d="M5 12h14" /><path d="m13 6 6 6-6 6" /></>);
export const FileIcon = icon(<><path d="M14 3H6v18h12V7z" /><path d="M14 3v4h4" /></>);
export const FolderIcon = icon(<path d="M3 6h7l2 2h9v11H3z" />);
export const DiagramIcon = icon(<><rect x="3" y="3" width="7" height="6" rx="1.5" /><rect x="14" y="15" width="7" height="6" rx="1.5" /><path d="M6.5 9v4a2 2 0 0 0 2 2H14" /></>);
export const GridIcon = icon(<><rect x="4" y="4" width="6" height="6" rx="1" /><rect x="14" y="4" width="6" height="6" rx="1" /><rect x="4" y="14" width="6" height="6" rx="1" /><rect x="14" y="14" width="6" height="6" rx="1" /></>);
export const MoreIcon = icon(<><circle cx="5" cy="12" r="1" /><circle cx="12" cy="12" r="1" /><circle cx="19" cy="12" r="1" /></>);
export const PencilIcon = icon(<><path d="M4 20h4L19 9l-4-4L4 16z" /><path d="m13.5 6.5 4 4" /></>);
export const TrashIcon = icon(<><path d="M4 7h16" /><path d="M9 7V4h6v3" /><path d="M6 7l1 13h10l1-13" /></>);
export const ZoomInIcon = icon(<><circle cx="11" cy="11" r="7" /><path d="m20 20-4-4" /><path d="M11 8v6M8 11h6" /></>);
export const ZoomOutIcon = icon(<><circle cx="11" cy="11" r="7" /><path d="m20 20-4-4" /><path d="M8 11h6" /></>);
export const FitIcon = icon(<><path d="M4 9V4h5" /><path d="M20 9V4h-5" /><path d="M4 15v5h5" /><path d="M20 15v5h-5" /></>);
export const CloudCheckIcon = icon(<><path d="M7 18a5 5 0 1 1 1.5-9.8A6 6 0 0 1 20 11a4 4 0 0 1-1 7.9" /><path d="m9.5 15 2 2 4-4" /></>);
export const CloudOffIcon = icon(<><path d="M7 18a5 5 0 0 1-1.5-9.8M10 6.2A6 6 0 0 1 20 11a4 4 0 0 1-.7 6.9" /><path d="m3 3 18 18" /></>);
export const AlertIcon = icon(<><path d="M12 3 2 20h20z" /><path d="M12 10v4" /><path d="M12 17h.01" /></>);
export const CheckIcon = icon(<path d="m5 12 5 5 9-10" />);
export const XIcon = icon(<><path d="M6 6l12 12" /><path d="M18 6 6 18" /></>);
export const SunIcon = icon(<><circle cx="12" cy="12" r="4" /><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" /></>);
export const MoonIcon = icon(<path d="M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z" />);
export const MenuIcon = icon(<><path d="M4 7h16" /><path d="M4 12h16" /><path d="M4 17h16" /></>);
export const CalendarIcon = icon(<><rect x="3" y="5" width="18" height="16" rx="2" /><path d="M3 10h18M8 3v4M16 3v4" /></>);
export const ShieldIcon = icon(<><path d="M12 3 4 6v6c0 5 3.5 8 8 9 4.5-1 8-4 8-9V6z" /><path d="m9 12 2 2 4-4" /></>);
export const SparkIcon = icon(<><path d="M12 3v4M12 17v4M3 12h4M17 12h4" /><path d="m6 6 2 2M16 16l2 2M6 18l2-2M16 8l2-2" /></>);
export const UndoIcon = icon(<><path d="M9 14 4 9l5-5" /><path d="M4 9h11a5 5 0 0 1 0 10h-3" /></>);

// Component types (spec 004 FR-010).
export const ClientIcon = icon(<><rect x="3" y="4" width="18" height="12" rx="2" /><path d="M8 20h8M12 16v4" /></>);
export const GatewayIcon = icon(<><path d="M4 12h5M15 12h5" /><path d="M9 6v12" /><path d="M15 6v12" /><path d="m17 9 3 3-3 3" /></>);
export const ServiceIcon = icon(<><path d="M12 2 20 7v10l-8 5-8-5V7z" /><path d="M12 12 20 7M12 12 4 7M12 12v10" /></>);
export const DatabaseIcon = icon(<><ellipse cx="12" cy="5.5" rx="7" ry="2.5" /><path d="M5 5.5v13c0 1.4 3.1 2.5 7 2.5s7-1.1 7-2.5v-13" /><path d="M5 12c0 1.4 3.1 2.5 7 2.5s7-1.1 7-2.5" /></>);
export const CacheIcon = icon(<path d="M13 2 4 14h7l-1 8 9-12h-7z" />);
export const QueueIcon = icon(<><rect x="3" y="7" width="4" height="10" rx="1" /><rect x="10" y="7" width="4" height="10" rx="1" /><rect x="17" y="7" width="4" height="10" rx="1" /></>);
export const BalancerIcon = icon(<><circle cx="12" cy="5" r="2.5" /><circle cx="5" cy="19" r="2.5" /><circle cx="19" cy="19" r="2.5" /><path d="M12 7.5v4M12 11.5 5 16.5M12 11.5l7 5" /></>);
export const ExternalIcon = icon(<><circle cx="12" cy="12" r="9" /><path d="M3 12h18" /><path d="M12 3a14 14 0 0 1 0 18M12 3a14 14 0 0 0 0 18" /></>);
