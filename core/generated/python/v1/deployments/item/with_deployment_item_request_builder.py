from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.base_request_builder import BaseRequestBuilder
from kiota_abstractions.base_request_configuration import RequestConfiguration
from kiota_abstractions.default_query_parameters import QueryParameters
from kiota_abstractions.get_path_parameters import get_path_parameters
from kiota_abstractions.method import Method
from kiota_abstractions.request_adapter import RequestAdapter
from kiota_abstractions.request_information import RequestInformation
from kiota_abstractions.request_option import RequestOption
from kiota_abstractions.serialization import Parsable, ParsableFactory
from typing import Any, Optional, TYPE_CHECKING, Union
from warnings import warn

if TYPE_CHECKING:
    from ....models.deployment_update import DeploymentUpdate
    from ....models.qyl.common.errors.internal_server_error import InternalServerError
    from ....models.qyl.common.errors.not_found_error import NotFoundError
    from ....models.qyl.common.errors.validation_error import ValidationError
    from ....models.qyl.domains.ops.deployment.deployment_entity import DeploymentEntity

class WithDeploymentItemRequestBuilder(BaseRequestBuilder):
    """
    Builds and executes requests for operations under /v1/deployments/{deploymentId}
    """
    def __init__(self,request_adapter: RequestAdapter, path_parameters: Union[str, dict[str, Any]]) -> None:
        """
        Instantiates a new WithDeploymentItemRequestBuilder and sets the default values.
        param path_parameters: The raw url or the url-template parameters for the request.
        param request_adapter: The request adapter to use to execute the requests.
        Returns: None
        """
        super().__init__(request_adapter, "{+baseurl}/v1/deployments/{deploymentId}", path_parameters)
    
    async def get(self,request_configuration: Optional[RequestConfiguration[QueryParameters]] = None) -> Optional[DeploymentEntity]:
        """
        Get deployment by ID
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: Optional[DeploymentEntity]
        """
        request_info = self.to_get_request_information(
            request_configuration
        )
        from ....models.qyl.common.errors.internal_server_error import InternalServerError
        from ....models.qyl.common.errors.not_found_error import NotFoundError
        from ....models.qyl.common.errors.validation_error import ValidationError

        error_mapping: dict[str, type[ParsableFactory]] = {
            "404": NotFoundError,
            "500": InternalServerError,
        }
        if not self.request_adapter:
            raise Exception("Http core is null") 
        from ....models.qyl.domains.ops.deployment.deployment_entity import DeploymentEntity

        return await self.request_adapter.send_async(request_info, DeploymentEntity, error_mapping)
    
    async def patch(self,body: DeploymentUpdate, request_configuration: Optional[RequestConfiguration[QueryParameters]] = None) -> Optional[DeploymentEntity]:
        """
        Update deployment status
        param body: Deployment update request
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: Optional[DeploymentEntity]
        """
        if body is None:
            raise TypeError("body cannot be null.")
        request_info = self.to_patch_request_information(
            body, request_configuration
        )
        from ....models.qyl.common.errors.internal_server_error import InternalServerError
        from ....models.qyl.common.errors.not_found_error import NotFoundError
        from ....models.qyl.common.errors.validation_error import ValidationError

        error_mapping: dict[str, type[ParsableFactory]] = {
            "400": ValidationError,
            "404": NotFoundError,
            "500": InternalServerError,
        }
        if not self.request_adapter:
            raise Exception("Http core is null") 
        from ....models.qyl.domains.ops.deployment.deployment_entity import DeploymentEntity

        return await self.request_adapter.send_async(request_info, DeploymentEntity, error_mapping)
    
    def to_get_request_information(self,request_configuration: Optional[RequestConfiguration[QueryParameters]] = None) -> RequestInformation:
        """
        Get deployment by ID
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: RequestInformation
        """
        request_info = RequestInformation(Method.GET, self.url_template, self.path_parameters)
        request_info.configure(request_configuration)
        request_info.headers.try_add("Accept", "application/json")
        return request_info
    
    def to_patch_request_information(self,body: DeploymentUpdate, request_configuration: Optional[RequestConfiguration[QueryParameters]] = None) -> RequestInformation:
        """
        Update deployment status
        param body: Deployment update request
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: RequestInformation
        """
        if body is None:
            raise TypeError("body cannot be null.")
        request_info = RequestInformation(Method.PATCH, self.url_template, self.path_parameters)
        request_info.configure(request_configuration)
        request_info.headers.try_add("Accept", "application/json")
        request_info.set_content_from_parsable(self.request_adapter, "application/json", body)
        return request_info
    
    def with_url(self,raw_url: str) -> WithDeploymentItemRequestBuilder:
        """
        Returns a request builder with the provided arbitrary URL. Using this method means any other path or query parameters are ignored.
        param raw_url: The raw URL to use for the request builder.
        Returns: WithDeploymentItemRequestBuilder
        """
        if raw_url is None:
            raise TypeError("raw_url cannot be null.")
        return WithDeploymentItemRequestBuilder(self.request_adapter, raw_url)
    
    @dataclass
    class WithDeploymentItemRequestBuilderGetRequestConfiguration(RequestConfiguration[QueryParameters]):
        """
        Configuration for the request such as headers, query parameters, and middleware options.
        """
        warn("This class is deprecated. Please use the generic RequestConfiguration class generated by the generator.", DeprecationWarning)
    
    @dataclass
    class WithDeploymentItemRequestBuilderPatchRequestConfiguration(RequestConfiguration[QueryParameters]):
        """
        Configuration for the request such as headers, query parameters, and middleware options.
        """
        warn("This class is deprecated. Please use the generic RequestConfiguration class generated by the generator.", DeprecationWarning)
    

