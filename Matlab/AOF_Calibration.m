clc
clear
close all

fileID = fopen('Curve_I_K_595.txt');
Curve595 = fscanf(fileID, '%f %f', [2, Inf]);
fclose(fileID);

fileID = fopen('Curve_I_K_515.txt');
Curve515 = fscanf(fileID, '%f %f', [2, Inf]);
fclose(fileID);

fileID = fopen('Curve_I_K_685.txt');
Curve685 = fscanf(fileID, '%f %f', [2, Inf]);
fclose(fileID);

fileID = fopen('Curve_I_K_631.txt');
Curve631 = fscanf(fileID, '%f %f', [2, Inf]);
fclose(fileID);

fileID = fopen('Curve_Maxes_2.txt');
Curve_Mxs = fscanf(fileID, '%f %f', [2, Inf]);
fclose(fileID);

fileID = fopen('Curve_WideSpec_lowPower.txt');
Curve_WideSpec = fscanf(fileID, '%f %f', [2, Inf]);
fclose(fileID);

fileID = fopen('CurveTest.txt');
CurveTest = fscanf(fileID, '%f %f %f', [3, Inf]);
fclose(fileID);

figure
plot(CurveTest(1,:), CurveTest(2,:));

fig1 = figure;
plot(Curve595(1,:), Curve595(2,:));
hold on
plot(Curve515(1,:), Curve515(2,:));
hold on
plot(Curve685(1,:), Curve685(2,:));
hold on
plot(Curve631(1,:), Curve631(2,:));
legend('595нм', '515нм' ,'685нм' , '631нм');
xlabel('Коэффициент ослабления К')
ylabel('I(K)');

figure
plot(Curve_Mxs(1,:), Curve_Mxs(2,:))
hold on
plot(Curve_WideSpec(1,:), Curve_WideSpec(2,:))
xlabel('\lambda')
ylabel('I(\lambda)')


I = Curve685(2,:);
k = Curve685(1,:);

Iopt = 1000;
n = 0;
a = 1;
b = 801;
c = floor((a+b)/2);

while n < 200
    n = n + 1;
    c = floor((a+b)/2);
    
    if I(c) < Iopt 
        a = c;
    else
        b = c;
    end
    
    if abs(b-a) < 10
        break;
    end
end

c = floor((a+b)/2);
k(c)
I(c)

figure(fig1);
hold on
plot(k(c), I(c), 'r--o');


