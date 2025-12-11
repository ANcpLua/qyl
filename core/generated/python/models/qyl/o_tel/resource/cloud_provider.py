from enum import Enum

class CloudProvider(str, Enum):
    Alibaba_cloud = "alibaba_cloud",
    Aws = "aws",
    Azure = "azure",
    Gcp = "gcp",
    Heroku = "heroku",
    Ibm_cloud = "ibm_cloud",
    Tencent_cloud = "tencent_cloud",

