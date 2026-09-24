# Running Docker - Via Any Terminal

## Building Docker Image
docker build -t mango-rewardapi-local:dev .

## Running The Container
docker run --name mango-rewardapi --env-file .env -p 5202:8080  mango-rewardapi-local:dev
