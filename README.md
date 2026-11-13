# Visual Search POC

A .NET console proof of concept for image understanding and vector search using
Amazon Bedrock, Amazon Rekognition and Amazon OpenSearch Service.

## What it demonstrates

- Describe an image with Amazon Nova Lite.
- Generate image embeddings with Titan Multimodal Embeddings.
- Extract labels, categories and parent labels using Rekognition.
- Create a 1024-dimensional vector index and store image metadata in OpenSearch.
- Reuse the repository's search, filtering and unit-of-work helpers in your own demo.

Local CLIP HTTP client helpers are also included. They expect separately hosted
services at `localhost:8080` and `localhost:8081`; those services are not included.

## Setup

Install the .NET 8 SDK. To print usage without credentials or cloud requests:

```sh
dotnet run --project VisualSearchPOC -- --help
```

For cloud commands, configure an AWS profile outside this repository, preferably
using AWS IAM Identity Center / SSO, or use an IAM role. The AWS SDK resolves
credentials through its default provider chain; no keys belong in source code.

```sh
aws configure sso --profile visual-search-demo
aws sso login --profile visual-search-demo
export AWS_PROFILE=visual-search-demo
export AWS_REGION=us-west-2
```

The selected account and region need access to the Bedrock models being used.
The default description model is `us.amazon.nova-lite-v1:0`; override it with
`BEDROCK_DESCRIPTION_MODEL_ID` for a compatible Amazon Nova model/profile.
`BEDROCK_CLAUDE_MODEL_ID` configures the optional Claude helper, which uses the
Anthropic messages schema rather than Nova's schema.

`.env.example` lists configuration placeholders. The app reads process environment
variables, not `.env` files; export settings in your shell or IDE.

## Run the demo

Use your own non-sensitive images. `Images/` is ignored by Git.

```sh
dotnet run --project VisualSearchPOC -- describe ./Images/sample.jpg
```

The description command prints the model's JSON response and does not require
OpenSearch. For indexing, provision an Amazon OpenSearch Service domain with
SigV4 authentication, network access from your machine and vector-index support:

```sh
export OPENSEARCH_ENDPOINT=https://your-domain.us-west-2.es.amazonaws.com
export OPENSEARCH_INDEX=visual-search-demo
dotnet run --project VisualSearchPOC -- index ./Images/sample.jpg demo-tenant demo-list
```

`AWS_REGION` must match the OpenSearch domain and support the required AWS models
and Rekognition. Indexing uses `amazon.titan-embed-image-v1` and Rekognition label
detection; use JPEG or PNG inputs supported by those services. It creates the
index if missing and writes a document with a new ID on every run.

**AWS calls incur costs.** Use a dedicated demo account/domain and least-privilege
IAM permissions scoped to the chosen models and index. Description requires
`bedrock:InvokeModel`; indexing additionally requires `rekognition:DetectLabels`
and the appropriate OpenSearch HTTP permissions/domain access policy. Cross-region
inference profiles may require access to their destination-region model resources.

## Public showcase and security

Current source uses configurable endpoints/model identifiers and caller-supplied
tenant/list IDs, not account-specific defaults. Never commit credentials, real
customer identifiers, private images, responses or logs. Ignore rules do not protect
files already tracked by Git.

This is a **local proof of concept, not a production service**. It has no web server,
authentication layer or enforced tenant authorization. Tenant IDs are metadata:
the unit of work preserves caller-supplied values rather than assigning a fixed
tenant, and unfiltered queries can access the whole index. Do not expose these
helpers through a public API without authentication, authorization, enforced tenant
isolation and limits on destructive operations.

Images are sent to AWS when cloud commands run. Optional helpers can print raw
model responses or include them in errors; do not publish sensitive console output.

Before making an existing repository public, review **all Git history**, not only
the latest files. Earlier commits may retain previous endpoints, account ARNs and
identifiers. Publish a fresh repository containing only the sanitized source, or
deliberately sanitize history before changing visibility. Rotate/revoke any actual
credentials ever committed; deleting them from the latest source is insufficient.
