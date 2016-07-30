import os

f = open("phrases-all.txt", "r")
v = open("ANC-all-count.txt", "r")

d = {}

c = 0
while True:
	s = v.readline()
	if len(s) == 0:
		break
	c += 1
	if c > 50000:
		break
	s = s[:-1]
	a = s.split(' ')
	d[a[0]] = a[1]
v.close()

c = 0
yesc = 0
o = open("phrases-all-filtered.txt", "w")
while True:
	s = f.readline()
	if len(s) == 0:
		break
	c += 1
	s = s[:-1]
	a = s.split(' ')
	flag = True
	for w in a:
		if not w in d:
			flag = False
	if flag:
		o.write(s + "\n")
		yesc += 1
print(c, yesc)
f.close()
o.close()
