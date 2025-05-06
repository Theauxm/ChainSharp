FROM mcr.microsoft.com/dotnet/sdk:8.0

# Install necessary packages
RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
    && apt-get -y install --no-install-recommends \
    git \
    procps \
    curl \
    wget \
    unzip \
    gnupg2 \
    lsb-release \
    ca-certificates \
    apt-transport-https \
    software-properties-common

# Clean up
RUN apt-get autoremove -y \
    && apt-get clean -y \
    && rm -rf /var/lib/apt/lists/*

# Set the working directory
WORKDIR /workspace
