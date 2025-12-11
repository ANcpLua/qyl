from __future__ import annotations
import datetime
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
    from ...models.deployment_create import DeploymentCreate
    from ...models.qyl.common.errors.internal_server_error import InternalServerError
    from ...models.qyl.common.errors.validation_error import ValidationError
    from ...models.qyl.domains.ops.deployment.deployment_entity import DeploymentEntity
    from ...models.qyl.domains.ops.deployment.deployment_environment import DeploymentEnvironment
    from ...models.qyl.domains.ops.deployment.deployment_status import DeploymentStatus
    from .deployments_get_response import DeploymentsGetResponse
    from .item.with_deployment_item_request_builder import WithDeploymentItemRequestBuilder
    from .metrics.metrics_request_builder import MetricsRequestBuilder

class DeploymentsRequestBuilder(BaseRequestBuilder):
    """
    Builds and executes requests for operations under /v1/deployments
    """
    def __init__(self,request_adapter: RequestAdapter, path_parameters: Union[str, dict[str, Any]]) -> None:
        """
        Instantiates a new DeploymentsRequestBuilder and sets the default values.
        param path_parameters: The raw url or the url-template parameters for the request.
        param request_adapter: The request adapter to use to execute the requests.
        Returns: None
        """
        super().__init__(request_adapter, "{+baseurl}/v1/deployments{?cursor,endTime,environment,limit,serviceName,startTime,status}", path_parameters)
    
    def by_deployment_id(self,deployment_id: str) -> WithDeploymentItemRequestBuilder:
        """
        Gets an item from the ApiSdk.v1.deployments.item collection
        param deployment_id: Unique identifier of the item
        Returns: WithDeploymentItemRequestBuilder
        """
        if deployment_id is None:
            raise TypeError("deployment_id cannot be null.")
        from .item.with_deployment_item_request_builder import WithDeploymentItemRequestBuilder

        url_tpl_params = get_path_parameters(self.path_parameters)
        url_tpl_params["deploymentId"] = deployment_id
        return WithDeploymentItemRequestBuilder(self.request_adapter, url_tpl_params)
    
    async def get(self,request_configuration: Optional[RequestConfiguration[DeploymentsRequestBuilderGetQueryParameters]] = None) -> Optional[DeploymentsGetResponse]:
        """
        List deployments
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: Optional[DeploymentsGetResponse]
        """
        request_info = self.to_get_request_information(
            request_configuration
        )
        from ...models.qyl.common.errors.internal_server_error import InternalServerError
        from ...models.qyl.common.errors.validation_error import ValidationError

        error_mapping: dict[str, type[ParsableFactory]] = {
            "400": ValidationError,
            "500": InternalServerError,
        }
        if not self.request_adapter:
            raise Exception("Http core is null") 
        from .deployments_get_response import DeploymentsGetResponse

        return await self.request_adapter.send_async(request_info, DeploymentsGetResponse, error_mapping)
    
    async def post(self,body: DeploymentCreate, request_configuration: Optional[RequestConfiguration[QueryParameters]] = None) -> Optional[DeploymentEntity]:
        """
        Record new deployment
        param body: Deployment creation request
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: Optional[DeploymentEntity]
        """
        if body is None:
            raise TypeError("body cannot be null.")
        request_info = self.to_post_request_information(
            body, request_configuration
        )
        from ...models.qyl.common.errors.internal_server_error import InternalServerError
        from ...models.qyl.common.errors.validation_error import ValidationError

        error_mapping: dict[str, type[ParsableFactory]] = {
            "400": ValidationError,
            "500": InternalServerError,
        }
        if not self.request_adapter:
            raise Exception("Http core is null") 
        from ...models.qyl.domains.ops.deployment.deployment_entity import DeploymentEntity

        return await self.request_adapter.send_async(request_info, DeploymentEntity, error_mapping)
    
    def to_get_request_information(self,request_configuration: Optional[RequestConfiguration[DeploymentsRequestBuilderGetQueryParameters]] = None) -> RequestInformation:
        """
        List deployments
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: RequestInformation
        """
        request_info = RequestInformation(Method.GET, self.url_template, self.path_parameters)
        request_info.configure(request_configuration)
        request_info.headers.try_add("Accept", "application/json")
        return request_info
    
    def to_post_request_information(self,body: DeploymentCreate, request_configuration: Optional[RequestConfiguration[QueryParameters]] = None) -> RequestInformation:
        """
        Record new deployment
        param body: Deployment creation request
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: RequestInformation
        """
        if body is None:
            raise TypeError("body cannot be null.")
        request_info = RequestInformation(Method.POST, self.url_template, self.path_parameters)
        request_info.configure(request_configuration)
        request_info.headers.try_add("Accept", "application/json")
        request_info.set_content_from_parsable(self.request_adapter, "application/json", body)
        return request_info
    
    def with_url(self,raw_url: str) -> DeploymentsRequestBuilder:
        """
        Returns a request builder with the provided arbitrary URL. Using this method means any other path or query parameters are ignored.
        param raw_url: The raw URL to use for the request builder.
        Returns: DeploymentsRequestBuilder
        """
        if raw_url is None:
            raise TypeError("raw_url cannot be null.")
        return DeploymentsRequestBuilder(self.request_adapter, raw_url)
    
    @property
    def metrics(self) -> MetricsRequestBuilder:
        """
        The metrics property
        """
        from .metrics.metrics_request_builder import MetricsRequestBuilder

        return MetricsRequestBuilder(self.request_adapter, self.path_parameters)
    
    @dataclass
    class DeploymentsRequestBuilderGetQueryParameters():
        """
        List deployments
        """
        def get_query_parameter(self,original_name: str) -> str:
            """
            Maps the query parameters names to their encoded names for the URI template parsing.
            param original_name: The original query parameter name in the class.
            Returns: str
            """
            if original_name is None:
                raise TypeError("original_name cannot be null.")
            if original_name == "end_time":
                return "endTime"
            if original_name == "service_name":
                return "serviceName"
            if original_name == "start_time":
                return "startTime"
            if original_name == "cursor":
                return "cursor"
            if original_name == "environment":
                return "environment"
            if original_name == "limit":
                return "limit"
            if original_name == "status":
                return "status"
            return original_name
        
        # Cursor
        cursor: Optional[str] = None

        # End time
        end_time: Optional[datetime.datetime] = None

        # Environment filter
        environment: Optional[DeploymentEnvironment] = None

        # Page size
        limit: Optional[int] = None

        # Service name filter
        service_name: Optional[str] = None

        # Start time
        start_time: Optional[datetime.datetime] = None

        # Status filter
        status: Optional[DeploymentStatus] = None

    
    @dataclass
    class DeploymentsRequestBuilderGetRequestConfiguration(RequestConfiguration[DeploymentsRequestBuilderGetQueryParameters]):
        """
        Configuration for the request such as headers, query parameters, and middleware options.
        """
        warn("This class is deprecated. Please use the generic RequestConfiguration class generated by the generator.", DeprecationWarning)
    
    @dataclass
    class DeploymentsRequestBuilderPostRequestConfiguration(RequestConfiguration[QueryParameters]):
        """
        Configuration for the request such as headers, query parameters, and middleware options.
        """
        warn("This class is deprecated. Please use the generic RequestConfiguration class generated by the generator.", DeprecationWarning)
    

