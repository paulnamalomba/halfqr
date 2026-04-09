import bcrypt

# Replace 'your_new_password' with the actual password you want to use
password = "driver-test.001".encode('utf-8')
hash = bcrypt.hashpw(password, bcrypt.gensalt())

print(hash.decode('utf-8'))