import type { ComponentType } from '../../shared/api/types';

/**
 * The eight component kinds (spec 004 FR-010, research R10): display name, accent and defaults.
 * Icons are looked up by type in `ComponentIcon`. Adding a type is one entry here (plus the server's list).
 */
export interface ComponentTypeInfo {
  type: ComponentType;
  label: string;
  /** design-system pastel used for the icon tile */
  pastel: 'blue' | 'purple' | 'green' | 'yellow' | 'orange' | 'pink';
  defaultTitle: string;
  defaultSize: { width: number; height: number };
}

export const COMPONENT_TYPES: ComponentTypeInfo[] = [
  { type: 'client', label: 'Client', pastel: 'blue', defaultTitle: 'Client', defaultSize: { width: 160, height: 100 } },
  { type: 'api_gateway', label: 'API Gateway', pastel: 'purple', defaultTitle: 'API Gateway', defaultSize: { width: 180, height: 100 } },
  { type: 'microservice', label: 'Microservice', pastel: 'green', defaultTitle: 'Service', defaultSize: { width: 180, height: 100 } },
  { type: 'database', label: 'Database', pastel: 'yellow', defaultTitle: 'Database', defaultSize: { width: 160, height: 100 } },
  { type: 'cache', label: 'Cache', pastel: 'orange', defaultTitle: 'Cache', defaultSize: { width: 160, height: 100 } },
  { type: 'message_queue', label: 'Message Queue', pastel: 'pink', defaultTitle: 'Queue', defaultSize: { width: 180, height: 100 } },
  { type: 'load_balancer', label: 'Load Balancer', pastel: 'blue', defaultTitle: 'Load Balancer', defaultSize: { width: 180, height: 100 } },
  { type: 'external_service', label: 'External Service', pastel: 'purple', defaultTitle: 'External Service', defaultSize: { width: 180, height: 100 } },
];

export const COMPONENT_TYPE_INFO = Object.fromEntries(COMPONENT_TYPES.map((t) => [t.type, t])) as Record<ComponentType, ComponentTypeInfo>;

// Limits shared with the server (DiagramLimits); the server re-checks everything (FR-033, FR-034).
export const LIMITS = {
  maxNodes: 500,
  maxEdges: 1000,
  maxTitle: 100,
  maxDescription: 2000,
  maxLabel: 200,
  maxMetadata: 20,
  maxMetadataKey: 50,
  maxMetadataValue: 500,
  minSize: 40,
  maxSize: 2000,
  maxCoordinate: 100_000,
  minZoom: 0.1,
  maxZoom: 4,
  grid: 20,
  maxDocumentBytes: 1024 * 1024,
} as const;

/** Drag-and-drop data type for palette items. */
export const DRAG_TYPE = 'application/x-devsync-component';
