PROJECT_NAME=

.PHONY: director
director:
	docker build -t gcr.io/$(PROJECT_NAME)/demo-director:latest MatchDirector/
	#docker push gcr.io/$(PROJECT_NAME)/demo-director:latest

.PHONY: matchfunction
matchfunction:
	docker build -t gcr.io/$(PROJECT_NAME)/demo-matchfunction:latest MatchFunction/
	docker push gcr.io/$(PROJECT_NAME)/demo-matchfunction:latest

.PHONY: frontend
frontend:
	docker build -t gcr.io/$(PROJECT_NAME)/demo-frontend:latest MatchFrontend/
	docker push gcr.io/$(PROJECT_NAME)/demo-frontend:latest

.PHONY: all
all: director matchfunction frontend
