<?php
$config = [
    'digest_alg'       => 'sha512',
    'private_key_bits' => 4096,
    'private_key_type' => OPENSSL_KEYTYPE_RSA,
];
$res = openssl_pkey_new($config);
if ($res === false) {
    echo "ERREUR: " . openssl_error_string() . PHP_EOL;
} else {
    echo "OpenSSL OK" . PHP_EOL;
}
