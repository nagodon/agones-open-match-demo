# Google Cloud Game ServersとOpenMatchのサンプル

このリポジトリはGoogle Cloud Game ServersとOpenMatchの.NET Core3.1による
実装のサンプルリポジトリです。

# 必要要件

- google-cloud-sdk
- docker
- dotnet-sdk

# 事前準備

## gcloudへのログイン

```
$ gcloud auth login
$ gcloud config set porject プロジェクト名
```

## SecretsManagerをプロジェクトで扱うので以下の権限を持つサービスアカウントの作成

```
$ export PROJECT=hoge
$ gcloud iam service-accounts create director --display-name "director-account"
$ gcloud projects add-iam-policy-binding $PROJECT --member='serviceAccount:director@${PROJECT}.iam.gserviceaccount.com' --role='roles/secretmanager.viewer'
```

# サンプルの動かし方

## リポジトリのクローン

```
$ export PROJECT=hoge
$ git clone https://github.com/nagodon/agones-open-match-demo.git
$ cd agones-open-match-demo
# DirectorのProgram.csのconstのProjectIdを自身のプロジェクトに置き換える
$ vim MatchDirector/Program.cs
# Directorで必要となる鍵を作成してダウンロードする
$ gcloud iam service-accounts keys create --iam-account director@${PROJECT}.iam.gserviceaccount.com MatchDirector/credential.json
$ cp solution.yaml{.example,}
$ vi solution.yaml # REPLACE_GCP_PROJECTとなってるいるところを自身のプロジェクト名に置き換える
```

## 各プロジェクトのビルド

```
$ make all PROJECT_NAME=GCPプロジェクト名
```

## GKEクラスタの作成

```
$ gcloud container clusters create agones-open-match-demo --release-channel=stable --tags=game-server --scopes=gke-default --num-nodes=1 --machine-type=n1-standard-4 --zone=asia-northeast1-a
$ gcloud container clusters get-credentials agones-open-match-demo --zone=asia-northeast1-a
```

## AgonesとOpenMatchのインストール

```
$ kubectl create namespace agones-system
$ kubectl apply -f https://raw.githubusercontent.com/googleforgames/agones/release-1.9.0/install/yaml/install.yaml
$ kubectl apply -f https://github.com/jetstack/cert-manager/releases/download/v1.0.4/cert-manager.yaml --validate=false
$ kubectl apply --namespace open-match -f https://open-match.dev/install/v1.0.0/yaml/01-open-match-core.yaml -f https://open-match.dev/install/v1.0.0/yaml/06-open-match-override-configmap.yaml -f https://open-match.dev/install/v1.0.0/yaml/07-open-match-default-evaluator.yaml
```

## Game Serversのクラスタの作成

※GameServerとしてsimple-udpを使用

```
$ gcloud game servers realms create demo-gcgs --time-zone Asia/Tokyo --location asia-northeast1
$ gcloud game servers clusters create demo-gcgs --realm=demo-gcgs --gke-cluster locations/asia-northeast1-a/clusters/agones-open-match-demo --namespace=default --location asia-northeast1 --no-dry-run
$ gcloud game servers deployments create demo-gcgs-deployment
$ gcloud game servers configs create fleet-spec --deployment demo-gcgs-deployment --fleet-configs-file fleet-spec.yaml
$ gcloud game servers deployments update-rollout demo-gcgs-deployment --default-config fleet-spec --no-dry-run
$ gcloud compute firewall-rules create game-server-firewall --allow udp:7000-8000 --target-tags game-server --description "Firewall to allow game server udp traffic"
```

## 証明書の設定

```
# 中でbase64コマンドを使ってるのでMacで行う場合はcoreutilsをダウンロードしてgbase64にコードを置き換えてください
$ ./agones-allocator-tls.sh
```

## カスタマイズしたOpenMatchのDirectorなどをインストール

```
$ kubectl create namespace demo
$ kubectl apply --namespace demo -f solution.yaml
```

## マッチングのチェック

２つのターミナルでそれぞれ以下コマンドを入力してマッチングをする事を確認する

```
# Aターミナル
$ FRONTEND_IP=$(kubectl get svc demo-frontend -n demo -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
$ ./test-matching.sh $FRONTEND_IP
Request match request ... bvg2va1ngcr7dj9flaig
Polling match request .. matched connection is x.x.x.x:7074
```

```
# Bターミナル
$ FRONTEND_IP=$(kubectl get svc demo-frontend -n demo -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
$ ./test-matching.sh $FRONTEND_IP
Request match request ... bvg2va1ngcr7dj9flat
Polling match request . matched connection is x.x.x.x:7074
```

```
# GameServerとの確認はncコマンドで確認
$ nc -u x.x.x.x 7074
HELO
ACK: HELO
EXIT
```
