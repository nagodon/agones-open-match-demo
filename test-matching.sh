#!/bin/bash -eu

HOST=$1

echo -n "Request match request ... "
TICKET_ID=$(curl -sS -XPOST -H"Content-Type: application/json" -d '{"GameMode":2, "PlayerId": "123456789"}' http://${HOST}:51507/match/request | jq -r '.ticketId')
echo $TICKET_ID

echo -n "Polling match request "
while true
do
  CONNECTION=$(curl -sS -XPOST -H"Content-Type: application/json" -d "{\"TicketId\": \"${TICKET_ID}\"}" http://${HOST}:51507/match/polling | jq -r '.connection')
  if [ "null" != "${CONNECTION}" ]; then
    echo " matched connection is ${CONNECTION}"
    break
  else
    echo -n "."
    sleep 5
  fi
done
