import type { ComponentType } from '../../shared/api/types';
import {
  BalancerIcon,
  CacheIcon,
  ClientIcon,
  DatabaseIcon,
  ExternalIcon,
  GatewayIcon,
  QueueIcon,
  ServiceIcon,
  type IconProps,
} from '../../shared/ui/icons';

const ICONS: Record<ComponentType, (props: IconProps) => React.ReactElement> = {
  client: ClientIcon,
  api_gateway: GatewayIcon,
  microservice: ServiceIcon,
  database: DatabaseIcon,
  cache: CacheIcon,
  message_queue: QueueIcon,
  load_balancer: BalancerIcon,
  external_service: ExternalIcon,
};

export function ComponentIcon({ type, size = 20 }: { type: ComponentType; size?: number }) {
  const Icon = ICONS[type] ?? ServiceIcon;
  return <Icon size={size} />;
}
