from __future__ import annotations
import datetime
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .cicd_event_name import CicdEventName
    from .cicd_pipeline_status import CicdPipelineStatus
    from .cicd_system import CicdSystem
    from .cicd_trigger_type import CicdTriggerType

@dataclass
class PipelineRunEvent(AdditionalDataHolder, Parsable):
    """
    Pipeline run event
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Pipeline name
    cicd_pipeline_name: Optional[str] = None
    # Pipeline run ID
    cicd_pipeline_run_id: Optional[str] = None
    # Duration in seconds
    duration_s: Optional[float] = None
    # Event name
    event_name: Optional[CicdEventName] = None
    # Pipeline status
    status: Optional[CicdPipelineStatus] = None
    # CI/CD system
    system: Optional[CicdSystem] = None
    # Event timestamp
    timestamp: Optional[datetime.datetime] = None
    # Trigger type
    trigger_type: Optional[CicdTriggerType] = None
    # Git branch
    vcs_repository_ref_name: Optional[str] = None
    # Git commit SHA
    vcs_repository_ref_revision: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> PipelineRunEvent:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: PipelineRunEvent
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return PipelineRunEvent()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .cicd_event_name import CicdEventName
        from .cicd_pipeline_status import CicdPipelineStatus
        from .cicd_system import CicdSystem
        from .cicd_trigger_type import CicdTriggerType

        from .cicd_event_name import CicdEventName
        from .cicd_pipeline_status import CicdPipelineStatus
        from .cicd_system import CicdSystem
        from .cicd_trigger_type import CicdTriggerType

        fields: dict[str, Callable[[Any], None]] = {
            "cicd.pipeline.name": lambda n : setattr(self, 'cicd_pipeline_name', n.get_str_value()),
            "cicd.pipeline.run.id": lambda n : setattr(self, 'cicd_pipeline_run_id', n.get_str_value()),
            "duration_s": lambda n : setattr(self, 'duration_s', n.get_float_value()),
            "event.name": lambda n : setattr(self, 'event_name', n.get_enum_value(CicdEventName)),
            "status": lambda n : setattr(self, 'status', n.get_enum_value(CicdPipelineStatus)),
            "system": lambda n : setattr(self, 'system', n.get_enum_value(CicdSystem)),
            "timestamp": lambda n : setattr(self, 'timestamp', n.get_datetime_value()),
            "trigger_type": lambda n : setattr(self, 'trigger_type', n.get_enum_value(CicdTriggerType)),
            "vcs.repository.ref.name": lambda n : setattr(self, 'vcs_repository_ref_name', n.get_str_value()),
            "vcs.repository.ref.revision": lambda n : setattr(self, 'vcs_repository_ref_revision', n.get_str_value()),
        }
        return fields
    
    def serialize(self,writer: SerializationWriter) -> None:
        """
        Serializes information the current object
        param writer: Serialization writer to use to serialize this model
        Returns: None
        """
        if writer is None:
            raise TypeError("writer cannot be null.")
        writer.write_str_value("cicd.pipeline.name", self.cicd_pipeline_name)
        writer.write_str_value("cicd.pipeline.run.id", self.cicd_pipeline_run_id)
        writer.write_float_value("duration_s", self.duration_s)
        writer.write_enum_value("event.name", self.event_name)
        writer.write_enum_value("status", self.status)
        writer.write_enum_value("system", self.system)
        writer.write_datetime_value("timestamp", self.timestamp)
        writer.write_enum_value("trigger_type", self.trigger_type)
        writer.write_str_value("vcs.repository.ref.name", self.vcs_repository_ref_name)
        writer.write_str_value("vcs.repository.ref.revision", self.vcs_repository_ref_revision)
        writer.write_additional_data_value(self.additional_data)
    

