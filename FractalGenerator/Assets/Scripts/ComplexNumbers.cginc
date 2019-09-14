#ifndef _COMPLEX_NUMBERS
#define _COMPLEX_NUMBERS

struct Complex {
    float real;
    float imaginary;
};

Complex CreateComplex(float real, float imaginary) {
    Complex z;
    z.real = real;
    z.imaginary = imaginary;
    return z;
}

float SquareMagnitude(Complex z) {
    return z.real * z.real + z.imaginary * z.imaginary;
}

float Magnitude(Complex z) {
    return sqrt(SquareMagnitude(z));
}

Complex Add(Complex a, Complex b) {
    return CreateComplex(a.real + b.real, a.imaginary + b.imaginary);
}

Complex Multiply(Complex a, Complex b) {
    float real = a.real * b.real - a.imaginary * b.imaginary;
    float imaginary = a.real * b.imaginary + a.imaginary * b.real;
    return CreateComplex(real, imaginary);
}

#endif