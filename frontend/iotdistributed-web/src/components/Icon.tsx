import type { ReactNode } from 'react'

export type IconName =
  | 'grid' | 'activity' | 'database' | 'settings' | 'refresh' | 'thermometer'
  | 'gauge' | 'waves' | 'circle' | 'check' | 'alert' | 'chevron' | 'clock'
  | 'download' | 'arrow' | 'server' | 'menu' | 'layers' | 'radio'

const drawings: Record<IconName, ReactNode> = {
  grid: <><rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/></>,
  activity: <><path d="M3 12h4l3-7 4 14 3-7h4"/></>,
  database: <><ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v7c0 1.7 3.6 3 8 3s8-1.3 8-3V5M4 12v7c0 1.7 3.6 3 8 3s8-1.3 8-3v-7"/></>,
  settings: <><circle cx="12" cy="12" r="3"/><path d="M12 2v3m0 14v3M2 12h3m14 0h3M4.9 4.9 7 7m10 10 2.1 2.1M19.1 4.9 17 7M7 17l-2.1 2.1"/></>,
  refresh: <><path d="M20 11a8 8 0 0 0-14-5L4 8m0-5v5h5M4 13a8 8 0 0 0 14 5l2-2m0 5v-5h-5"/></>,
  thermometer: <><path d="M10 14.4V5a3 3 0 0 1 6 0v9.4a5 5 0 1 1-6 0Z"/><path d="M13 9v9"/></>,
  gauge: <><path d="M4.5 17a9 9 0 1 1 15 0"/><path d="m12 13 4-5"/><circle cx="12" cy="13" r="1.3"/><path d="M7 18h10"/></>,
  waves: <><path d="M3 8c2.1 0 2.1-2 4.2-2S9.3 8 11.4 8s2.1-2 4.2-2S17.7 8 21 8M3 14c2.1 0 2.1-2 4.2-2s2.1 2 4.2 2 2.1-2 4.2-2 2.1 2 5.4 2M3 20c2.1 0 2.1-2 4.2-2s2.1 2 4.2 2 2.1-2 4.2-2 2.1 2 5.4 2"/></>,
  circle: <><circle cx="12" cy="12" r="8"/></>,
  check: <><path d="m5 12 4 4 10-10"/></>,
  alert: <><path d="m12 3 10 18H2L12 3Z"/><path d="M12 9v5m0 3h.01"/></>,
  chevron: <><path d="m9 6 6 6-6 6"/></>,
  clock: <><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></>,
  download: <><path d="M12 3v12m-4-4 4 4 4-4M4 17v4h16v-4"/></>,
  arrow: <><path d="M5 12h14m-6-6 6 6-6 6"/></>,
  server: <><rect x="3" y="3" width="18" height="7" rx="2"/><rect x="3" y="14" width="18" height="7" rx="2"/><path d="M7 6.5h.01M7 17.5h.01M11 6.5h6M11 17.5h6"/></>,
  menu: <><path d="M4 7h16M4 12h16M4 17h16"/></>,
  layers: <><path d="m12 2 10 6-10 6L2 8l10-6Zm-10 11 10 6 10-6M2 18l10 5 10-5"/></>,
  radio: <><circle cx="12" cy="12" r="2"/><path d="M7.2 7.2a7 7 0 0 0 0 9.6m9.6 0a7 7 0 0 0 0-9.6M4.2 4.2a11 11 0 0 0 0 15.6m15.6 0a11 11 0 0 0 0-15.6"/></>,
}

export default function Icon({ name, size = 20, className = '' }: { name: IconName; size?: number; className?: string }) {
  return <svg className={className} width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{drawings[name]}</svg>
}
