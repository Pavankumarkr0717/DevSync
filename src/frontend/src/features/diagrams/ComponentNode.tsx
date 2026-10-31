import { Handle, NodeResizer, Position, type Node, type NodeProps } from '@xyflow/react';
import type { DiagramNodeDoc } from '../../shared/api/types';
import { useCanvasActions } from './canvasContext';
import { ComponentIcon } from './ComponentIcon';
import { COMPONENT_TYPE_INFO, LIMITS } from './componentTypes';

export type ComponentFlowNode = Node<{ doc: DiagramNodeDoc }, 'component'>;

/**
 * Every component type is drawn by this one renderer, driven by the type registry (research R10):
 * a card with a type icon, the title and the first line of the description (reference "happy path"
 * cards). Text is rendered as text, never markup.
 */
export function ComponentNode({ id, data, selected }: NodeProps<ComponentFlowNode>) {
  const { editable, resized } = useCanvasActions();
  const info = COMPONENT_TYPE_INFO[data.doc.type];
  const subtitle = data.doc.description.split('\n')[0] || info?.label;

  return (
    <>
      <NodeResizer
        isVisible={editable && selected}
        minWidth={LIMITS.minSize}
        minHeight={LIMITS.minSize}
        maxWidth={LIMITS.maxSize}
        maxHeight={LIMITS.maxSize}
        color="var(--accent)"
        onResizeEnd={(_event, box) => resized(id, box)}
      />
      <Handle type="target" position={Position.Top} isConnectable={editable} />
      <Handle type="target" position={Position.Left} id="left" isConnectable={editable} />
      <div className="component-node" data-type={data.doc.type}>
        <span className={`component-icon pastel-${info?.pastel ?? 'blue'}`}>
          <ComponentIcon type={data.doc.type} />
        </span>
        <span className="component-title">{data.doc.title}</span>
        {subtitle && <span className="component-subtitle">{subtitle}</span>}
      </div>
      <Handle type="source" position={Position.Bottom} isConnectable={editable} />
      <Handle type="source" position={Position.Right} id="right" isConnectable={editable} />
    </>
  );
}
