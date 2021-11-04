FROM alpine:3.13 AS grpc-build

WORKDIR /opt

RUN apk add --update alpine-sdk autoconf libtool linux-headers cmake git && \
    \
    git clone -b v1.36.4 https://github.com/grpc/grpc --depth 1 --shallow-submodules && \
    cd grpc && git submodule update --init --depth 1 && \
    \
    mkdir -p cmake/build && cd cmake/build && \
    \
    cmake -DCMAKE_BUILD_TYPE=RelWithDebInfo \
        -DgRPC_BACKWARDS_COMPATIBILITY_MODE=ON \
        -DgRPC_BUILD_TESTS=OFF \
        ../.. && \
    \
    make grpc_csharp_ext -j4 && \
    \
    mkdir -p /out && cp libgrpc_csharp_ext.* /out

FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine3.13 as build-env

# Setup Dotnet Core tools global path
ENV PATH="${PATH}:/root/.dotnet/tools"

# Install runtime dependencies
RUN \
    apk update \
    && apk --no-cache add \
        binutils \
        # Install bash
        bash \
        bash-completion \
        bash-doc \
        # Install extensions dependencies
        curl \
        git \
        libxml2-dev \
        openldap-dev \
        openssh \
        unzip \
        wget \
    \
    # Install dotnet tools globally
    && dotnet tool install --global Paket --version 6.1.3 \
    && dotnet tool install --global fake-cli --version 5.20.4 \
    ;

# build scripts
COPY ./build.sh /app-build/
COPY ./build.fsx /app-build/
COPY ./paket.dependencies /app-build/
COPY ./paket.references /app-build/
COPY ./paket.lock /app-build/

# sources
COPY ./KafkaExample.fsproj /app-build/
COPY ./src /app-build/src

# others
COPY ./.git /app-build/.git
COPY ./.config /app-build/.config

WORKDIR /app-build

RUN ./build.sh -t Release no-clean

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:5.0-alpine3.13

# Install runtime dependencies
RUN \
    apk update \
    && apk --no-cache add \
        binutils \
        # Install bash
        bash \
        bash-completion \
        bash-doc \
        # Install extensions dependencies
        curl \
    \
    ;

# Fix for a bug in grpc library on alpine docker - https://github.com/grpc/grpc/issues/21446
COPY --from=grpc-build /out/libgrpc_csharp_ext.so /app/libgrpc_csharp_ext.x64.so

WORKDIR /app
COPY --from=build-env /app .

ENTRYPOINT ["dotnet", "KafkaExample.dll"]
