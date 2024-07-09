FROM ubuntu/dotnet-runtime AS builder
WORKDIR /app
COPY . .
RUN dotnet build --configuration Release

FROM registry.access.redhat.com/ubi8/ubi-minimal as nativebuilder

RUN mkdir -p /tmp/ssl \
&& cp /usr/lib64/libstdc++.so.6 /tmp/ssl/libstdc++.so.6 \
&& cp /usr/lib64/libgcc_s.so.1 /tmp/ssl/libgcc_s.so.1 \
&& cp /usr/lib64/libz.so.1 /tmp/ssl/libz.so.1

FROM gcr.io/distroless/base-debian12:latest-amd64

COPY --from=nativebuilder /tmp/ssl/ /
ENV LD_LIBRARY_PATH /
COPY --from=builder /app/bin/release/utsuki-bot-net utsuki-bot-net
CMD ["./utsuki-bot-net"]