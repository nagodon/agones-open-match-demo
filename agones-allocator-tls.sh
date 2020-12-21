#!/bin/bash -u

KEY_FILE=client.key
CERT_FILE=client.crt
TLS_CA_FILE=ca.crt
EXTERNAL_IP=$(kubectl get services agones-allocator -n agones-system -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
REGION=asia-northeast2

if [ ! -f "${KEY_FILE}" ]; then
  openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout ${KEY_FILE} -out ${CERT_FILE}
fi

# Create a self-signed ClusterIssuer
cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: selfsigned
spec:
  selfSigned: {}
EOF

# Create a Certificate with IP for the allocator-tls secret
cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: allocator-tls
  namespace: agones-system
spec:
  commonName: ${EXTERNAL_IP}
  ipAddresses:
    - ${EXTERNAL_IP}
  secretName: allocator-tls
  issuerRef:
    name: selfsigned
    kind: ClusterIssuer
EOF

CERT_FILE_VALUE=$(cat ${CERT_FILE} | base64 -w 0)
TLS_CA_VALUE=$(kubectl get secret allocator-tls -n agones-system -ojsonpath='{.data.ca\.crt}')
KEY_FILE_VALUE=$(cat ${KEY_FILE} | base64 -w 0)

kubectl get secret allocator-tls-ca -o json -n agones-system | jq '.data["tls-ca.crt"]="'${TLS_CA_VALUE}'"' | kubectl apply -f -
kubectl get secret allocator-client-ca -o json -n agones-system | jq '.data["client_trial.crt"]="'${CERT_FILE_VALUE}'"' | kubectl apply -f -

SECRET_JSON="{\"Ip\":\"${EXTERNAL_IP}\",\"ClientKey\":\"${KEY_FILE_VALUE}\",\"ClientCert\":\"${CERT_FILE_VALUE}\",\"TlsCert\":\"${TLS_CA_VALUE}\"}"
echo ${SECRET_JSON} | gcloud secrets create agones-allocator-info --data-file=- --replication-policy user-managed --locations ${REGION}
