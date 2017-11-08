docker rm nginx_luajit -f
docker build -f ubuntu.dockerfile -t nginx-luajit-ubuntu .
docker run -ti -d --name nginx_luajit -p 8080:80 -p 7777:7777 -p 7711:7711 -v $(pwd)/logs:/nginx-1.11.9/1.11.9/logs nginx-luajit-ubuntu